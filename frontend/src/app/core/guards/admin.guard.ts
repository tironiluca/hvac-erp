import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { CurrentUserService } from '../services/current-user.service';

/** Restricts a route to the current tenant's Admins; sends everyone else back to the dashboard. Use after `authGuard`. */
export const adminGuard: CanActivateFn = async () => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);

  await currentUser.ensureLoaded();
  return currentUser.isAdmin() ? true : router.createUrlTree(['/']);
};

/** Restricts a route to platform administrators (settings shared by every tenant). Use after `authGuard`. */
export const platformAdminGuard: CanActivateFn = async () => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);

  await currentUser.ensureLoaded();
  return currentUser.isPlatformAdmin() ? true : router.createUrlTree(['/']);
};

/**
 * Keeps a signed-in user who belongs to no tenant out of the app: every API call would be refused.
 * A profile that failed to load is let through — the API stays the judge, and a network blip must
 * not lock anyone out. Use after `authGuard`.
 */
export const memberGuard: CanActivateFn = async () => {
  const currentUser = inject(CurrentUserService);
  const router = inject(Router);

  await currentUser.ensureLoaded();
  return currentUser.hasAccess() ? true : router.createUrlTree(['/no-access']);
};
