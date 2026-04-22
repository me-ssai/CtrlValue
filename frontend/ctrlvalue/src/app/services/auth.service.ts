import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, switchMap, of } from 'rxjs';
import { environment } from '@env/environment';
import {
    AuthResponse, LoginRequest, RegisterRequest, UserInfo,
    UpdateProfileRequest, ChangePasswordRequest, UpdateProfileResponse
} from '../models/api.models';
import { EntityService } from './entity.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly USER_KEY = 'ctrlvalue_user';

    private currentUserSubject = new BehaviorSubject<UserInfo | null>(this.getStoredUser());
    currentUser$ = this.currentUserSubject.asObservable();

    private entityService = inject(EntityService);

    constructor(private http: HttpClient) { }

    // isAuthenticated is based on in-memory user presence (populated on login / app reload from localStorage).
    // Actual token validity is enforced by the backend via httpOnly cookie; 401 responses trigger re-login.
    // In demo mode, always treat as authenticated — no real user or token is needed.
    get isAuthenticated(): boolean {
        if (environment.demo) return true;
        return !!this.currentUserSubject.value;
    }
    get currentUser(): UserInfo | null { return this.currentUserSubject.value; }

    register(request: RegisterRequest): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, request);
    }

    login(request: LoginRequest): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
            tap(response => this.handleAuthResponse(response)),
            switchMap(response =>
                this.entityService.getOrCreateDefaultEntity().pipe(
                    switchMap(() => of(response))
                )
            )
        );
    }

    verifyEmail(token: string): Observable<any> {
        return this.http.get(`${environment.apiUrl}/auth/verify-email`, { params: { token } });
    }

    resendVerification(email: string): Observable<any> {
        return this.http.post(`${environment.apiUrl}/auth/resend-verification`, { email });
    }

    updateProfile(request: UpdateProfileRequest): Observable<UpdateProfileResponse> {
        return this.http.put<UpdateProfileResponse>(`${environment.apiUrl}/auth/profile`, request).pipe(
            tap(response => { if (response.user) this.updateStoredUser(response.user); })
        );
    }

    changePassword(request: ChangePasswordRequest): Observable<any> {
        return this.http.put(`${environment.apiUrl}/auth/password`, request);
    }

    completeOnboarding(): Observable<any> {
        return this.http.post(`${environment.apiUrl}/auth/onboarding/complete`, {}).pipe(
            tap(() => {
                const user = this.currentUserSubject.value;
                if (user) {
                    const updated = { ...user, onboardingCompleted: true };
                    localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
                    this.currentUserSubject.next(updated);
                }
            })
        );
    }

    get isOnboardingComplete(): boolean {
        if (environment.demo) return true;
        return this.currentUserSubject.value?.onboardingCompleted === true;
    }

    refreshToken(): Observable<AuthResponse> {
        // Tokens are in httpOnly cookies — no body needed; backend reads cookies directly.
        return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, {}).pipe(
            tap(response => this.handleAuthResponse(response))
        );
    }

    logout(): void {
        // Backend clears httpOnly cookies; frontend clears non-sensitive profile data.
        this.http.post(`${environment.apiUrl}/auth/logout`, {}).subscribe({ error: () => {} });
        localStorage.removeItem(this.USER_KEY);
        this.currentUserSubject.next(null);
        this.entityService.clearCurrentEntity();
    }

    getUserRole(): string | null {
        return this.currentUserSubject.value?.role ?? null;
    }

    private handleAuthResponse(response: AuthResponse): void {
        // Tokens are set as httpOnly cookies by the backend — not present in response body.
        // Only store non-sensitive user profile data in localStorage.
        if (response.user) {
            localStorage.setItem(this.USER_KEY, JSON.stringify(response.user));
            this.currentUserSubject.next(response.user);
        }
    }

    private updateStoredUser(user: UserInfo): void {
        localStorage.setItem(this.USER_KEY, JSON.stringify(user));
        this.currentUserSubject.next(user);
    }

    private getStoredUser(): UserInfo | null {
        const userJson = localStorage.getItem(this.USER_KEY);
        return userJson ? JSON.parse(userJson) : null;
    }
}
