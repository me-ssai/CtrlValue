import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { EntityService } from './entity.service';
import { of } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, UserInfo } from '../models/api.models';

describe('AuthService', () => {
    let service: AuthService;
    let httpMock: HttpTestingController;
    let entityServiceSpy: jasmine.SpyObj<EntityService>;

    const mockUser: UserInfo = {
        id: 'user-123',
        email: 'test@test.com',
        firstName: 'Test',
        lastName: 'User',
        isEmailConfirmed: true,
        role: 'User',
        onboardingCompleted: true
    };

    const mockAuthResponse: AuthResponse = {
        token: '',
        refreshToken: '',
        expiration: new Date().toISOString(),
        user: mockUser,
        requiresEmailVerification: false
    };

    beforeEach(() => {
        entityServiceSpy = jasmine.createSpyObj('EntityService', [
            'getOrCreateDefaultEntity',
            'clearCurrentEntity'
        ]);
        entityServiceSpy.getOrCreateDefaultEntity.and.returnValue(of({} as any));

        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [
                AuthService,
                { provide: EntityService, useValue: entityServiceSpy }
            ]
        });

        service = TestBed.inject(AuthService);
        httpMock = TestBed.inject(HttpTestingController);

        localStorage.clear();
    });

    afterEach(() => {
        httpMock.verify();
        localStorage.clear();
    });

    // ── register ──────────────────────────────────────────────────────────────

    it('should call POST /auth/register with the request payload', () => {
        const request = { email: 'new@example.com', password: 'Password1!', firstName: 'New', lastName: 'User' };

        service.register(request).subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/register`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(request);
        req.flush({ ...mockAuthResponse, requiresEmailVerification: true });
    });

    // ── login ─────────────────────────────────────────────────────────────────

    it('should call POST /auth/login and store user on success', () => {
        service.login({ email: 'test@test.com', password: 'Password1!' }).subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
        expect(req.request.method).toBe('POST');
        req.flush(mockAuthResponse);

        expect(localStorage.getItem('ctrlvalue_user')).not.toBeNull();
        expect(service.currentUser?.email).toBe('test@test.com');
    });

    it('should call getOrCreateDefaultEntity after successful login', () => {
        service.login({ email: 'test@test.com', password: 'Password1!' }).subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
        req.flush(mockAuthResponse);

        expect(entityServiceSpy.getOrCreateDefaultEntity).toHaveBeenCalled();
    });

    // ── logout ────────────────────────────────────────────────────────────────

    it('should clear user from memory and localStorage on logout', () => {
        localStorage.setItem('ctrlvalue_user', JSON.stringify(mockUser));

        service.logout();

        // Consume the fire-and-forget logout request
        const req = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
        req.flush({});

        expect(service.currentUser).toBeNull();
        expect(localStorage.getItem('ctrlvalue_user')).toBeNull();
    });

    it('should call clearCurrentEntity on logout', () => {
        service.logout();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/logout`);
        req.flush({});

        expect(entityServiceSpy.clearCurrentEntity).toHaveBeenCalled();
    });

    // ── isAuthenticated ───────────────────────────────────────────────────────

    it('should return false when no user in memory', () => {
        // Ensure no user stored and in-memory subject is cleared
        localStorage.removeItem('ctrlvalue_user');
        (service as any).currentUserSubject.next(null);
        expect(service.isAuthenticated).toBeFalse();
    });

    // ── updateProfile ─────────────────────────────────────────────────────────

    it('should call PUT /auth/profile and update stored user', () => {
        localStorage.setItem('ctrlvalue_user', JSON.stringify(mockUser));
        (service as any).currentUserSubject.next(mockUser);

        service.updateProfile({ firstName: 'Updated', lastName: 'Name' }).subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/profile`);
        expect(req.request.method).toBe('PUT');
        req.flush({ user: { ...mockUser, firstName: 'Updated', lastName: 'Name' } });

        expect(service.currentUser?.firstName).toBe('Updated');
    });

    // ── refreshToken ──────────────────────────────────────────────────────────

    it('should call POST /auth/refresh', () => {
        service.refreshToken().subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/refresh`);
        expect(req.request.method).toBe('POST');
        req.flush(mockAuthResponse);
    });

    // ── verifyEmail ───────────────────────────────────────────────────────────

    it('should call GET /auth/verify-email with token query param', () => {
        service.verifyEmail('abc123').subscribe();

        const req = httpMock.expectOne(r => r.url.includes('/auth/verify-email'));
        expect(req.request.method).toBe('GET');
        expect(req.request.params.get('token')).toBe('abc123');
        req.flush({ message: 'Verified' });
    });

    // ── completeOnboarding ────────────────────────────────────────────────────

    it('should update onboardingCompleted flag after completeOnboarding', () => {
        (service as any).currentUserSubject.next({ ...mockUser, onboardingCompleted: false });
        localStorage.setItem('ctrlvalue_user', JSON.stringify({ ...mockUser, onboardingCompleted: false }));

        service.completeOnboarding().subscribe();

        const req = httpMock.expectOne(`${environment.apiUrl}/auth/onboarding/complete`);
        req.flush({});

        expect(service.currentUser?.onboardingCompleted).toBeTrue();
    });
});
