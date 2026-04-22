import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { environment } from '../../environments/environment';

/**
 * Guard that blocks navigation to restricted routes in demo mode.
 * Routes protected by this guard (Settings, Connections, Admin) are
 * redirected to the dashboard instead.
 */
export const demoBlockedGuard: CanActivateFn = () => {
    if (!environment.demo) return true;
    inject(Router).navigate(['/dashboard']);
    return false;
};
