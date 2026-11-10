import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CurrentUser } from '../models/branding.model';
import { TenantSelectionService } from './tenant-selection.service';

/**
 * The signed-in caller's profile (`GET /api/me`): role, the tenant and store this session works
 * in, and the tenants / stores they can switch to. Gates role-dependent UI and feeds the switcher.
 * Fetched at most once per session.
 *
 * Signing in grants nothing by itself: a user becomes a member of a tenant when an administrator
 * adds them. Until then `hasAccess()` is false and the app shows the no-access page.
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private http = inject(HttpClient);
  private selection = inject(TenantSelectionService);

  private userSignal = signal<CurrentUser | null>(null);
  user = this.userSignal.asReadonly();

  readonly role = computed(() => this.userSignal()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly isPlatformAdmin = computed(() => this.userSignal()?.isPlatformAdmin ?? false);

  /** False once the profile is loaded and the user belongs to no tenant. */
  readonly hasAccess = computed(() => {
    const user = this.userSignal();
    return user === null || user.tenantId !== null;
  });

  readonly tenants = computed(() => this.userSignal()?.tenants ?? []);
  readonly currentTenant = computed(() => {
    const user = this.userSignal();
    return user?.tenants.find((t) => t.id === user.tenantId) ?? null;
  });
  /** The stores of the current tenant the user may work in. */
  readonly stores = computed(() => this.currentTenant()?.stores ?? []);
  /** The store new quotes and repairs go to; null while "all stores" is selected among several. */
  readonly currentStore = computed(() => {
    const storeId = this.userSignal()?.storeId ?? null;
    return this.stores().find((s) => s.id === storeId) ?? null;
  });
  /** Whether there is anything to switch between. */
  readonly canSwitch = computed(() => this.tenants().length > 1 || this.stores().length > 1);

  private inFlight?: Promise<CurrentUser | null>;

  /** Resolves once the profile is loaded (or a load has failed); cached for the session. */
  ensureLoaded(): Promise<CurrentUser | null> {
    if (this.userSignal()) {
      return Promise.resolve(this.userSignal());
    }
    this.inFlight ??= this.load()
      .then((user) => {
        this.userSignal.set(user);
        return user;
      })
      .catch(() => null)
      .finally(() => (this.inFlight = undefined));
    return this.inFlight;
  }

  /** Forgets the cached profile and the remembered tenant / store (on sign-out). */
  clear(): void {
    this.userSignal.set(null);
    this.selection.clear();
  }

  private async load(): Promise<CurrentUser> {
    const fetch = () => firstValueFrom(this.http.get<CurrentUser>(`${environment.apiUrl}/me`));

    const user = await fetch();
    // A remembered tenant or store the user may no longer use resolves to no access at all (the
    // API never falls back silently). Forget it and ask again for the default.
    const remembered = this.selection.tenantId() !== null || this.selection.storeId() !== null;
    if (user.tenantId === null && remembered) {
      this.selection.clear();
      return fetch();
    }
    return user;
  }
}
