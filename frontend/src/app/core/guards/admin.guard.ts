import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CurrentUserService } from '../services/current-user.service';

/** Restricts a route to Admin users; sends everyone else back to the dashboard. Use after `authGuard`. */
export const adminGuard: CanActivateFn = async () => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);

  await currentUser.ensureLoaded();
  return currentUser.isAdmin() ? true : router.createUrlTree(['/']);
};
