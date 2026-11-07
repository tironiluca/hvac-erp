import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
  isDevMode,
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TranslocoHttpLoader } from './transloco-loader';
import { provideTransloco } from '@jsverse/transloco';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { forbiddenInterceptor } from './core/interceptors/forbidden.interceptor';
import { correlationInterceptor } from './core/interceptors/correlation.interceptor';
import { ReportingErrorHandler } from './core/services/error-reporting';
import { BrandingService } from './core/services/branding.service';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: ErrorHandler, useClass: ReportingErrorHandler },
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([correlationInterceptor, authInterceptor, forbiddenInterceptor])),
    // Theme the app from the stored white-label branding before the first route renders
    // (login page included). Tolerant of a slow/unreachable API — falls back to the SCSS theme.
    provideAppInitializer(() => inject(BrandingService).loadForBootstrap()),
    provideTransloco({
      config: {
        availableLangs: ['en', 'it'],
        // A deployment's language for users who have not picked one (HVAC_DEFAULT_LANGUAGE).
        defaultLang: environment.defaultLanguage,
        // Remove this option if your application doesn't support changing language in runtime.
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
  ],
};
