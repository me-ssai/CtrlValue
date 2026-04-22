import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginComponent } from './login.component';
import { AuthService } from '../../services/auth.service';
import { Router, ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { AuthResponse } from '../../models/api.models';

describe('LoginComponent', () => {
    let component: LoginComponent;
    let fixture: ComponentFixture<LoginComponent>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let snackBarSpy: jasmine.SpyObj<MatSnackBar>;

    const mockSuccessResponse: AuthResponse = {
        token: '',
        refreshToken: '',
        expiration: new Date().toISOString(),
        user: { id: 'u1', email: 'test@test.com', firstName: 'Test', lastName: 'User', isEmailConfirmed: true, role: 'User', onboardingCompleted: true },
        requiresEmailVerification: false
    };

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['login'], {
            isAuthenticated: false,
            isOnboardingComplete: true
        });
        snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

        await TestBed.configureTestingModule({
            imports: [LoginComponent, RouterTestingModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: MatSnackBar, useValue: snackBarSpy },
                { provide: ActivatedRoute, useValue: {
                    snapshot: { queryParamMap: { get: () => null } }
                }}
            ],
            schemas: [NO_ERRORS_SCHEMA]
        })
        // Override at component level so standalone component picks up the spy
        .overrideComponent(LoginComponent, {
            add: { providers: [
                { provide: MatSnackBar, useValue: snackBarSpy },
                { provide: AuthService, useValue: authServiceSpy }
            ]}
        })
        .compileComponents();

        routerSpy = TestBed.inject(Router) as jasmine.SpyObj<Router>;
        spyOn(routerSpy, 'navigate');

        fixture   = TestBed.createComponent(LoginComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should start on credentials step', () => {
        expect(component.step).toBe('credentials');
    });

    it('should NOT redirect to /dashboard when not authenticated on creation', () => {
        // authServiceSpy.isAuthenticated is false in beforeEach — no redirect expected
        expect(routerSpy.navigate).not.toHaveBeenCalledWith(['/dashboard']);
    });

    it('should show snackBar when submitting empty credentials', () => {
        component.email = '';
        component.password = '';

        component.onSubmitCredentials();

        expect(snackBarSpy.open).toHaveBeenCalledWith('Please fill in all fields', 'Close', jasmine.any(Object));
        expect(authServiceSpy.login).not.toHaveBeenCalled();
    });

    it('should call authService.login with correct credentials', () => {
        authServiceSpy.login.and.returnValue(of(mockSuccessResponse));
        component.email    = 'test@test.com';
        component.password = 'Password1!';

        component.onSubmitCredentials();

        expect(authServiceSpy.login).toHaveBeenCalledWith({
            email:    'test@test.com',
            password: 'Password1!'
        });
    });

    it('should transition to notice step on successful login', () => {
        authServiceSpy.login.and.returnValue(of(mockSuccessResponse));
        component.email    = 'test@test.com';
        component.password = 'Password1!';

        component.onSubmitCredentials();

        expect(component.step).toBe('notice');
    });

    it('should show error snackBar on login failure', () => {
        authServiceSpy.login.and.returnValue(throwError(() => ({
            status: 400,
            error: { message: 'Invalid credentials' }
        })));
        component.email    = 'wrong@test.com';
        component.password = 'WrongPassword1!';

        component.onSubmitCredentials();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            'Invalid credentials', 'Close', jasmine.any(Object)
        );
    });

    it('should show rate limit message on 429 error', () => {
        authServiceSpy.login.and.returnValue(throwError(() => ({ status: 429, error: {} })));
        component.email    = 'limited@test.com';
        component.password = 'Password1!';

        component.onSubmitCredentials();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            jasmine.stringContaining('Too many login attempts'), 'Close', jasmine.any(Object)
        );
    });

    it('should set loading to true during login request', () => {
        let capturedLoading: boolean | undefined;
        authServiceSpy.login.and.callFake(() => {
            capturedLoading = component.loading;
            return of(mockSuccessResponse);
        });
        component.email    = 'test@test.com';
        component.password = 'Password1!';

        component.onSubmitCredentials();

        expect(capturedLoading).toBeTrue();
        expect(component.loading).toBeFalse(); // reset after response
    });

    // ── Navigation ────────────────────────────────────────────────────────────

    it('should navigate to /dashboard on acknowledgeNotice', () => {
        component.acknowledgeNotice();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard']);
    });

    // ── Email verified query param ────────────────────────────────────────────

    it('should show verified snackBar when ?verified=true is in URL', () => {
        // Directly invoke ngOnInit with verified=true query param
        const routeWithVerified = { snapshot: { queryParamMap: { get: (key: string) => key === 'verified' ? 'true' : null } } };
        (component as any).route = routeWithVerified;
        component.ngOnInit();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            jasmine.stringContaining('Email verified'), 'Close', jasmine.any(Object)
        );
    });
});
