import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  NgZone,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { BreakpointObserver } from '@angular/cdk/layout';
import { map } from 'rxjs';
import { DOCUMENT } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavContainer, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from '../services/auth.service';
import { BrandingService } from '../services/branding.service';
import { CurrentUserService } from '../services/current-user.service';
import { LanguageService } from '../services/language.service';
import { TenantSelectionService } from '../services/tenant-selection.service';
import { ThemeService } from '../services/theme.service';
import { visibleNavItems } from './nav-items';

/** At or below this width the rail turns into a full-screen overlay drawer. */
const MOBILE_QUERY = '(max-width: 959.98px)';

/** localStorage flag for the collapsed (mini) rail. */
const RAIL_COLLAPSED_KEY = 'hvac-nav-collapsed';

@Component({
  selector: 'hvac-shell',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    MatDividerModule,
    TranslocoModule,
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent implements AfterViewInit {
  protected auth = inject(AuthService);
  protected currentUser = inject(CurrentUserService);
  protected theme = inject(ThemeService);
  protected branding = inject(BrandingService);
  protected language = inject(LanguageService);
  private zone = inject(NgZone);
  private selection = inject(TenantSelectionService);
  private document = inject(DOCUMENT);

  @ViewChild(MatSidenavContainer) private sidenavContainer!: MatSidenavContainer;

  /**
   * Desktop-only: collapse the rail to an icon-only strip. Persisted per browser so
   * the choice survives reloads; ignored on mobile, where the rail is an overlay.
   */
  protected readonly railCollapsed = signal(localStorage.getItem(RAIL_COLLAPSED_KEY) === '1');

  /**
   * True while the viewport is narrow enough that the sidenav should overlay the
   * content instead of sitting beside it. Seeded synchronously from `matchMedia`
   * so the first paint already has the right layout (no desktop→mobile flash).
   */
  protected readonly isMobile = toSignal(
    inject(BreakpointObserver)
      .observe(MOBILE_QUERY)
      .pipe(map((state) => state.matches)),
    { initialValue: matchMedia(MOBILE_QUERY).matches },
  );

  /** Nav entries, minus the ones the signed-in user's role does not open. */
  protected readonly navItems = computed(() =>
    visibleNavItems({
      isAdmin: this.currentUser.isAdmin(),
      isPlatformAdmin: this.currentUser.isPlatformAdmin(),
    }),
  );

  constructor() {
    void this.currentUser.ensureLoaded();
  }

  ngAfterViewInit(): void {
    // A `side` drawer only re-measures the content offset on open/close, not on a
    // CSS width change, so seed it once for the restored collapsed state.
    queueMicrotask(() => this.sidenavContainer?.updateContentMargins());
  }

  toggleRail(): void {
    const next = !this.railCollapsed();
    this.railCollapsed.set(next);
    localStorage.setItem(RAIL_COLLAPSED_KEY, next ? '1' : '0');

    // The rail width animates in CSS; nudge the sidenav container every frame for
    // the length of that transition so the routed content edge tracks it instead
    // of jumping once at the end.
    this.zone.runOutsideAngular(() => {
      const start = performance.now();
      const tick = () => {
        this.sidenavContainer?.updateContentMargins();
        if (performance.now() - start < 260) requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    });
  }

  /**
   * Works in another company, or narrows to one store (`null`: every store the user may work in).
   * Everything on screen belongs to the previous choice, so the app restarts on the dashboard
   * instead of patching each list and the live calendar connection in place.
   */
  switchTo(tenantId: number, storeId: number | null): void {
    const user = this.currentUser.user();
    if (user?.tenantId === tenantId && this.selection.storeId() === storeId) {
      return;
    }
    this.selection.select(tenantId, storeId);
    this.document.location.assign('/');
  }

  /** True when no single store is selected among several. */
  protected readonly allStores = computed(
    () => this.currentUser.stores().length > 1 && this.selection.storeId() === null,
  );

  signOut(): void {
    this.currentUser.clear();
    void this.auth.signOut();
  }

  toggleTheme(): void {
    this.theme.toggle();
  }

  setLanguage(lang: string): void {
    this.language.set(lang);
  }
}
