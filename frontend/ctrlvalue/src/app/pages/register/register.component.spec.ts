import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RegisterComponent } from './register.component';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { AuthResponse } from '../../models/api.models';

describe('RegisterComponent', () => {
    let component: RegisterComponent;
    let fixture: ComponentFixture<RegisterComponent>;
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;
    let snackBarSpy: jasmine.SpyObj<MatSnackBar>;

    const mockVerificationResponse: AuthResponse = {
        token: '',
        refreshToken: '',
        expiration: new Date().toISOString(),
        user: { id: 'u1', email: 'new@gmail.com', firstName: 'New', lastName: 'User', isEmailConfirmed: false, role: 'User', onboardingCompleted: false },
        requiresEmailVerification: true
    };

    beforeEach(async () => {
        authServiceSpy = jasmine.createSpyObj('AuthService', ['register'], {
            isAuthenticated: false
        });
        snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

        await TestBed.configureTestingModule({
            imports: [RegisterComponent, RouterTestingModule],
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: MatSnackBar, useValue: snackBarSpy }
            ],
            schemas: [NO_ERRORS_SCHEMA]
        })
        .overrideComponent(RegisterComponent, {
            add: { providers: [
                { provide: MatSnackBar, useValue: snackBarSpy },
                { provide: AuthService, useValue: authServiceSpy }
            ]}
        })
        .compileComponents();

        routerSpy = TestBed.inject(Router) as jasmine.SpyObj<Router>;
        spyOn(routerSpy, 'navigate');

        fixture   = TestBed.createComponent(RegisterComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });

    it('should NOT redirect when not authenticated on creation', () => {
        expect(routerSpy.navigate).not.toHaveBeenCalledWith(['/dashboard']);
    });

    // ── Validation ────────────────────────────────────────────────────────────

    it('should show error when required fields are empty', () => {
        component.firstName = '';
        component.lastName  = '';
        component.email     = '';
        component.password  = '';

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith('Please fill in all fields', 'Close', jasmine.any(Object));
        expect(authServiceSpy.register).not.toHaveBeenCalled();
    });

    it('should show error when passwords do not match', () => {
        component.firstName       = 'Test';
        component.lastName        = 'User';
        component.email           = 'test@gmail.com';
        component.password        = 'Password1!';
        component.confirmPassword = 'Different1!';

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith('Passwords do not match', 'Close', jasmine.any(Object));
        expect(authServiceSpy.register).not.toHaveBeenCalled();
    });

    it('should show error when password is too short', () => {
        component.firstName       = 'Test';
        component.lastName        = 'User';
        component.email           = 'test@gmail.com';
        component.password        = 'Pw1!';
        component.confirmPassword = 'Pw1!';

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            jasmine.stringContaining('at least 8 characters'), 'Close', jasmine.any(Object)
        );
        expect(authServiceSpy.register).not.toHaveBeenCalled();
    });

    // ── Successful Registration ───────────────────────────────────────────────

    it('should call authService.register with correct payload', () => {
        authServiceSpy.register.and.returnValue(of(mockVerificationResponse));
        setValidForm();

        component.onSubmit();

        expect(authServiceSpy.register).toHaveBeenCalledWith({
            email:     'new@gmail.com',
            password:  'Password1!',
            firstName: 'New',
            lastName:  'User'
        });
    });

    it('should show verification message and redirect to /login on success with email verification', () => {
        authServiceSpy.register.and.returnValue(of(mockVerificationResponse));
        setValidForm();

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            jasmine.stringContaining('verify your account'), 'Close', jasmine.any(Object)
        );
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('should redirect to /dashboard if registration returns no verification required', () => {
        authServiceSpy.register.and.returnValue(of({
            ...mockVerificationResponse,
            requiresEmailVerification: false
        }));
        setValidForm();

        component.onSubmit();

        expect(routerSpy.navigate).toHaveBeenCalledWith(['/dashboard']);
    });

    // ── Error Handling ────────────────────────────────────────────────────────

    it('should show error snackBar on registration failure', () => {
        authServiceSpy.register.and.returnValue(throwError(() => ({
            status: 400,
            error: { message: 'Email already in use' }
        })));
        setValidForm();

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            'Email already in use', 'Close', jasmine.any(Object)
        );
    });

    it('should show fallback error message when error has no detail', () => {
        authServiceSpy.register.and.returnValue(throwError(() => ({ status: 500, error: {} })));
        setValidForm();

        component.onSubmit();

        expect(snackBarSpy.open).toHaveBeenCalledWith(
            jasmine.stringContaining('failed'), 'Close', jasmine.any(Object)
        );
    });

    // ── Loading State ─────────────────────────────────────────────────────────

    it('should set loading to true during registration request', () => {
        let capturedLoading: boolean | undefined;
        authServiceSpy.register.and.callFake(() => {
            capturedLoading = component.loading;
            return of(mockVerificationResponse);
        });
        setValidForm();

        component.onSubmit();

        expect(capturedLoading).toBeTrue();
        expect(component.loading).toBeFalse();
    });

    // ── Helpers ───────────────────────────────────────────────────────────────

    function setValidForm(): void {
        component.firstName       = 'New';
        component.lastName        = 'User';
        component.email           = 'new@gmail.com';
        component.password        = 'Password1!';
        component.confirmPassword = 'Password1!';
    }
});
