/** Mirrors the backend `PortalBrandingDto` (`GET /api/branding`). */
export interface PortalBranding {
  primarySeed: string;
  tertiarySeed: string;
  neutralSeed: string;
  portalTitle: string;
  logoUrl?: string | null;
  logoDarkUrl?: string | null;
  faviconUrl?: string | null;
  loginBackgroundUrl?: string | null;
  updatedAt: string;
}

/** `PUT /api/branding` — seeds + title only; assets are set through their own upload endpoint. */
export interface UpdatePortalBrandingRequest {
  primarySeed: string;
  tertiarySeed: string;
  neutralSeed: string;
  portalTitle: string;
}

export type BrandingAssetKind = 'logo' | 'logo-dark' | 'favicon' | 'login-background';

export interface BrandingAsset {
  url: string;
}

export type UserRole = 'Admin' | 'Sales' | 'Warehouse';

/** A store of a tenant, as listed in the switcher. */
export interface StoreSummary {
  id: number;
  name: string;
  city: string | null;
}

/** A tenant (company) the signed-in user belongs to, with the stores they may work in. */
export interface TenantAccess {
  id: number;
  name: string;
  role: UserRole;
  stores: StoreSummary[];
}

/**
 * `GET /api/me` — who the caller is, what this session works in and what else they can switch to.
 * `role`, `tenantId` and `storeId` are null for someone who has not been given access to any
 * company yet; `storeId` is also null when several stores are open and none is selected.
 */
export interface CurrentUser {
  email: string;
  role: UserRole | null;
  isPlatformAdmin: boolean;
  tenantId: number | null;
  storeId: number | null;
  tenants: TenantAccess[];
}
