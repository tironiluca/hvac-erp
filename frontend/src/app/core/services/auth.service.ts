import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { createClient, SupabaseClient, User, Session, AuthError } from '@supabase/supabase-js';
import { environment } from '../../../environments/environment';
import { SnackbarService } from './snackbar.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private router = inject(Router);
  private snackbar = inject(SnackbarService);

  private supabase: SupabaseClient;
  private initialized = false;

  private currentUserSignal = signal<User | null>(null);
  private currentSessionSignal = signal<Session | null>(null);
  private loadingSignal = signal<boolean>(true);

  currentUser = this.currentUserSignal.asReadonly();
  currentSession = this.currentSessionSignal.asReadonly();
  loading = this.loadingSignal.asReadonly();
  isAuthenticated = computed(() => this.currentUserSignal() !== null);

  constructor() {
    this.supabase = createClient(environment.supabase.url, environment.supabase.publishableKey, {
      auth: {
        persistSession: true,
        storage: localStorage,
        autoRefreshToken: true,
        detectSessionInUrl: true,
      },
    });

    this.initializeAuthState();
  }

  /** The API upserts the AppUser profile (role, installer link) on first authenticated request. */
  private async initializeAuthState(): Promise<void> {
    try {
      const {
        data: { session },
      } = await this.supabase.auth.getSession();

      if (session) {
        this.currentSessionSignal.set(session);
        this.currentUserSignal.set(session.user);
      }

      this.supabase.auth.onAuthStateChange((event, session) => {
        this.currentSessionSignal.set(session);
        this.currentUserSignal.set(session?.user ?? null);

        if (!this.initialized) {
          return;
        }

        if (event === 'SIGNED_OUT') {
          this.router.navigate(['/auth/login']);
        }
      });
    } finally {
      this.loadingSignal.set(false);
      this.initialized = true;
    }
  }

  async signIn(email: string, password: string): Promise<{ success: boolean; error?: string }> {
    const { data, error } = await this.supabase.auth.signInWithPassword({ email, password });

    if (error) {
      const message = this.getAuthErrorMessage(error);
      this.snackbar.error(message);
      return { success: false, error: message };
    }

    return { success: !!data.session };
  }

  /** Ends the Supabase session; surfaces a snackbar on failure. */
  async signOut(): Promise<void> {
    const { error } = await this.supabase.auth.signOut();
    if (error) {
      this.snackbar.error(this.getAuthErrorMessage(error));
      return;
    }

    this.currentUserSignal.set(null);
    this.currentSessionSignal.set(null);
  }

  /** For other services that need to talk to Supabase directly (Storage uploads, etc.). */
  getSupabaseClient(): SupabaseClient {
    return this.supabase;
  }

  private getAuthErrorMessage(error: AuthError): string {
    switch (error.message) {
      case 'Invalid login credentials':
        return 'Email o password non corretti';
      case 'Email not confirmed':
        return 'Verifica il tuo indirizzo email prima di accedere';
      default:
        return error.message || 'Si è verificato un errore di autenticazione';
    }
  }
}
