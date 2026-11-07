import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CurrentUser } from '../models/branding.model';

/**
 * The signed-in caller's app profile (`GET /api/me`), used to gate admin-only UI (the branding
 * editor, its nav entry). Fetched at most once per session; the API upserts the `AppUser` row on the
 * first authenticated request, so this is safe to call as soon as a session exists.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private http = inject(HttpClient);

  private userSignal = signal<CurrentUser | null>(null);
  user = this.userSignal.asReadonly();

  readonly role = computed(() => this.userSignal()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');

  private inFlight?: Promise<CurrentUser | null>;

  /** Resolves once the profile is loaded (or a load has failed); cached for the session. */
  ensureLoaded(): Promise<CurrentUser | null> {
    if (this.userSignal()) {
      return Promise.resolve(this.userSignal());
    }
    this.inFlight ??= firstValueFrom(this.http.get<CurrentUser>(`${environment.apiUrl}/me`))
      .then((user) => {
        this.userSignal.set(user);
        return user;
      })
      .catch(() => null)
      .finally(() => (this.inFlight = undefined));
    return this.inFlight;
  }

  /** Forgets the cached profile (on sign-out). */
  clear(): void {
    this.userSignal.set(null);
  }
}
