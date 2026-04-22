import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Role-based route guard. Pass an allowed role ('SuperAdmin' or 'SiteAdmin').
 * SuperAdmins can access SiteAdmin routes too.
 *
 * Usage in routes:
 *   canActivate: [authGuard, roleGuard('SuperAdmin')]
 */
export const roleGuard = (requiredRole: 'SuperAdmin' | 'SiteAdmin'): CanActivateFn => {
    return () => {
        const authService = inject(AuthService);
        const router = inject(Router);

        const userRole = authService.getUserRole();

        // SuperAdmin can access everything
        if (userRole === 'SuperAdmin') return true;

        // SiteAdmin can only access SiteAdmin-level routes
        if (requiredRole === 'SiteAdmin' && userRole === 'SiteAdmin') return true;

        router.navigate(['/dashboard']);
        return false;
    };
};
