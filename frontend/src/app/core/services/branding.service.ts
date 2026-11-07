import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BrandingAsset,
  BrandingAssetKind,
  PortalBranding,
  UpdatePortalBrandingRequest,
} from '../models/branding.model';
import { BrandSeeds, buildSysColorVars, DEFAULT_BRAND_SEEDS, primaryTone } from '../utils/m3-theme.util';
import { ThemeService } from './theme.service';

const DEFAULT_TITLE = 'HVAC ERP';

/**
 * Fetches the portal's white-label branding and applies it at runtime: the `--mat-sys-*` colour
 * tokens (generated from the three brand seeds for the active light/dark mode) go on `<html>` as
 * inline properties that override the compile-time `styles.scss` theme; the title, favicon and the
 * `--hvac-login-bg` backdrop are updated to match. Loaded once at bootstrap via an app initializer
 * (see `app.config.ts`) so the login page and first paint are already themed.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private http = inject(HttpClient);
  private theme = inject(ThemeService);
  private baseUrl = `${environment.apiUrl}/branding`;

  private brandingSignal = signal<PortalBranding | null>(null);
  branding = this.brandingSignal.asReadonly();

  /** Unsaved seeds/title being previewed in the editor; overrides the stored record while set. */
  private previewSignal = signal<Partial<PortalBranding> | null>(null);

  private root = document.documentElement;

  readonly portalTitle = computed(() => this.effective().portalTitle || DEFAULT_TITLE);
  readonly logoUrl = computed(() => {
    const b = this.effective();
    return (this.theme.mode() === 'dark' && b.logoDarkUrl) || b.logoUrl || null;
  });
  readonly loginBackgroundUrl = computed(() => this.effective().loginBackgroundUrl ?? null);

  constructor() {
    // Re-apply whenever the record, an in-flight preview, or the light/dark mode changes.
    effect(() => {
      this.brandingSignal();
      this.previewSignal();
      this.theme.mode();
      this.apply();
    });
  }

  /** Fetches the stored branding (`GET /api/branding`, anonymous) and applies it. */
  load(): Observable<PortalBranding> {
    return this.http
      .get<PortalBranding>(this.baseUrl)
      .pipe(tap((branding) => this.brandingSignal.set(branding)));
  }

  /** Blocking load for the app initializer — tolerant of a slow/unreachable API. */
  async loadForBootstrap(): Promise<void> {
    try {
      await firstValueFrom(this.load());
    } catch {
      // Leave the compile-time SCSS theme in place.
    }
  }

  /** Saves branding (Admin only) and drops any in-flight preview. */
  update(request: UpdatePortalBrandingRequest): Observable<PortalBranding> {
    return this.http.put<PortalBranding>(this.baseUrl, request).pipe(
      tap((branding) => {
        this.brandingSignal.set(branding);
        this.previewSignal.set(null);
      }),
    );
  }

  /** Uploads a logo / dark logo / favicon / login background (Admin only, 5 MB max). */
  uploadAsset(kind: BrandingAssetKind, file: File): Observable<BrandingAsset> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<BrandingAsset>(`${this.baseUrl}/assets/${kind}`, form).pipe(
      tap((asset) => {
        const current = this.brandingSignal();
        if (current) {
          this.brandingSignal.set({ ...current, ...assetPatch(kind, asset.url) });
        }
      }),
    );
  }

  /** Live-preview unsaved seeds/title (no server call). Pass `null` to drop the preview. */
  preview(patch: Partial<PortalBranding> | null): void {
    this.previewSignal.set(patch);
  }

  /** Discard any preview and re-fetch the saved record. */
  revert(): Observable<PortalBranding> {
    this.previewSignal.set(null);
    return this.load();
  }

  private effective(): PortalBranding {
    const base: PortalBranding = this.brandingSignal() ?? {
      primarySeed: DEFAULT_BRAND_SEEDS.primary,
      tertiarySeed: DEFAULT_BRAND_SEEDS.tertiary,
      neutralSeed: DEFAULT_BRAND_SEEDS.neutral,
      portalTitle: DEFAULT_TITLE,
      updatedAt: '',
    };
    const preview = this.previewSignal();
    return preview ? { ...base, ...preview } : base;
  }

  private apply(): void {
    const b = this.effective();
    const seeds: BrandSeeds = {
      primary: b.primarySeed,
      tertiary: b.tertiarySeed,
      neutral: b.neutralSeed,
    };
    const isDark = this.theme.mode() === 'dark';

    const vars = buildSysColorVars(seeds, isDark);
    for (const [name, value] of Object.entries(vars)) {
      this.root.style.setProperty(name, value);
    }

    const bg = b.loginBackgroundUrl
      ? `#000 url("${b.loginBackgroundUrl}") center / cover no-repeat`
      : `linear-gradient(135deg, ${primaryTone(seeds, isDark ? 25 : 92)}, ${primaryTone(seeds, isDark ? 10 : 98)})`;
    this.root.style.setProperty('--hvac-login-bg', bg);

    document.title = b.portalTitle || DEFAULT_TITLE;
    this.applyFavicon(b.faviconUrl ?? null);
  }

  private applyFavicon(url: string | null): void {
    let link = document.querySelector<HTMLLinkElement>('link[rel~="icon"]');
    if (!link) {
      link = document.createElement('link');
      link.rel = 'icon';
      document.head.appendChild(link);
    }
    link.href = url ?? 'favicon.ico';
  }
}

function assetPatch(kind: BrandingAssetKind, url: string): Partial<PortalBranding> {
  switch (kind) {
    case 'logo':
      return { logoUrl: url };
    case 'logo-dark':
      return { logoDarkUrl: url };
    case 'favicon':
      return { faviconUrl: url };
    case 'login-background':
      return { loginBackgroundUrl: url };
  }
}
