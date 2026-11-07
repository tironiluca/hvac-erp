import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { firstValueFrom, filter, timeout, catchError, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

const waitForAuthReady = (auth: AuthService): Promise<void> =>
  firstValueFrom(
    toObservable(auth.loading).pipe(
      filter((loading) => !loading),
      timeout({ each: 5000 }),
      catchError(() => of(false)),
    ),
  ).then(() => void 0);

/** Protects routes that require a signed-in user; redirects to /auth/login otherwise. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.loading()) {
    await waitForAuthReady(auth);
  }

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
  }

  return true;
};

/** Keeps signed-in users off the login page. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.loading()) {
    await waitForAuthReady(auth);
  }

  if (auth.isAuthenticated()) {
    return router.createUrlTree(['/']);
  }

  return true;
};
