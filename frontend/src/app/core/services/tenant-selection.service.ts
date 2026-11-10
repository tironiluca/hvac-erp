import { Injectable, signal } from '@angular/core';

const TENANT_KEY = 'hvac-tenant';
const STORE_KEY = 'hvac-store';

function readId(key: string): number | null {
  const id = Number(localStorage.getItem(key));
  return Number.isInteger(id) && id > 0 ? id : null;
}

function writeId(key: string, id: number | null): void {
  if (id === null) {
    localStorage.removeItem(key);
  } else {
    localStorage.setItem(key, String(id));
  }
}

/**
 * The tenant (company) and store this browser works in, remembered across reloads. Sent to the API
 * as `X-Tenant-Id` / `X-Store-Id` by `tenantInterceptor`; the API checks both against the user's
 * memberships, so this is a preference, never a permission.
 *
 * Nothing selected means "my first company, every store I may work in" — the right default for the
 * common single-company, single-store user, who never sees the switcher.
 */
@Injectable({ providedIn: 'root' })
export class TenantSelectionService {
  private readonly tenant = signal<number | null>(readId(TENANT_KEY));
  private readonly store = signal<number | null>(readId(STORE_KEY));

  readonly tenantId = this.tenant.asReadonly();
  readonly storeId = this.store.asReadonly();

  /** Remembers the tenant and, optionally, one of its stores (null: every store). */
  select(tenantId: number | null, storeId: number | null): void {
    this.tenant.set(tenantId);
    this.store.set(storeId);
    writeId(TENANT_KEY, tenantId);
    writeId(STORE_KEY, storeId);
  }

  /** Forgets the selection (sign-out, or a remembered choice the user may no longer use). */
  clear(): void {
    this.select(null, null);
  }
}
