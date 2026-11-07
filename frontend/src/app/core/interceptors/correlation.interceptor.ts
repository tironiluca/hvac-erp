import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

/** Header the API echoes back and stamps on every log line / problem response for the request. */
export const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * Tags each call to our API with a fresh correlation id, so an error a user reports can be matched to
 * the exact server log lines (and Sentry event) for that request.
 */
export const correlationInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl) || req.headers.has(CORRELATION_HEADER)) {
    return next(req);
  }
  const id = `spa-${crypto.randomUUID().replace(/-/g, '')}`;
  return next(req.clone({ setHeaders: { [CORRELATION_HEADER]: id } }));
};
