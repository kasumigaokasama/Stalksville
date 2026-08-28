import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

/** ADMIN-only routes (admin console); everyone else lands on the dashboard. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.canAdmin() ? true : router.createUrlTree(['/dashboard']);
};
