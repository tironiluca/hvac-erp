import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { TranslocoService } from '@jsverse/transloco';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SnackbarService } from '../services/snackbar.service';

/**
 * Surfaces a role-based refusal (HTTP 403 from the API's per-action policies) as a single,
 * consistent snackbar, so a feature whose button a role cannot use never fails silently. The
 * error is still re-thrown for callers that want to reset their own spinner state.
 */
export const forbiddenInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const snackbar = inject(SnackbarService);
  const transloco = inject(TranslocoService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 403) {
        snackbar.error(transloco.translate('common.forbidden'));
      }
      return throwError(() => error);
    }),
  );
};
