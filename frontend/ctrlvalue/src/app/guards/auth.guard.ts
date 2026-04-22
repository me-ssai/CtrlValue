import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { environment } from '../../environments/environment';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route) => {
    // Demo mode: skip all auth checks — the app loads anonymously with seeded data
    if (environment.demo) return true;

    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated) {
        router.navigate(['/login']);
        return false;
    }

    // Redirect new users to onboarding (unless they're already going there)
    const targetPath = route.routeConfig?.path ?? '';
    if (!authService.isOnboardingComplete && targetPath !== 'onboarding') {
        router.navigate(['/onboarding']);
        return false;
    }

    return true;
};
