import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    function createGuardResult(
        isAuthenticated: boolean,
        isOnboardingComplete: boolean,
        targetPath = 'dashboard'
    ) {
        authServiceSpy = jasmine.createSpyObj('AuthService', [], {
            isAuthenticated,
            isOnboardingComplete
        });

        routerSpy = jasmine.createSpyObj('Router', ['navigate']);

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        });

        const route = { routeConfig: { path: targetPath } } as unknown as ActivatedRouteSnapshot;

        return TestBed.runInInjectionContext(() => authGuard(route, {} as any));
    }

    it('should allow navigation when authenticated and onboarding complete', () => {
        const result = createGuardResult(true, true);

        expect(result).toBeTrue();
        expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('should redirect to /login when not authenticated', () => {
        const result = createGuardResult(false, true);

        expect(result).toBeFalse();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should redirect to /onboarding when authenticated but onboarding not complete', () => {
        const result = createGuardResult(true, false, 'dashboard');

        expect(result).toBeFalse();
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/onboarding']);
    });

    it('should allow navigation to /onboarding even when onboarding is not complete', () => {
        const result = createGuardResult(true, false, 'onboarding');

        expect(result).toBeTrue();
        expect(routerSpy.navigate).not.toHaveBeenCalled();
    });
});
