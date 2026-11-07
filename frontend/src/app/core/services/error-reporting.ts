import { ErrorHandler, Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

type Capture = (error: unknown) => void;
let capture: Capture | undefined;

/**
 * Starts Sentry error reporting when `environment.sentryDsn` is set; otherwise a no-op. The SDK is
 * loaded as a lazy chunk, so builds without a DSN ship none of it. The browser SDK sends no PII by default.
 */
export async function initErrorReporting(): Promise<void> {
  if (!environment.sentryDsn) return;
  try {
    const Sentry = await import('@sentry/angular');
    Sentry.init({
      dsn: environment.sentryDsn,
      environment: environment.production ? 'production' : 'development',
    });
    capture = (error) => Sentry.captureException(error);
  } catch (error) {
    console.warn('Error reporting could not start', error);
  }
}

/** Angular's global error sink: logs to the console as before and forwards to Sentry once loaded. */
@Injectable()
export class ReportingErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    console.error(error);
    capture?.(error);
  }
}
