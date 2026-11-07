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

/** `GET /api/me` — used to gate admin-only UI. */
export interface CurrentUser {
  email: string;
  role: 'Admin' | 'Sales' | 'Warehouse';
}
