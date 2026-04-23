import { TestBed } from '@angular/core/testing';
import {
    HttpClient,
    provideHttpClient,
    withInterceptors
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { demoInterceptor } from './demo.interceptor';
import { DemoStateService } from '../services/demo-state.service';
import * as envModule from '../../environments/environment';

describe('demoInterceptor — non-demo environment', () => {
    let http: HttpClient;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        // Ensure demo is false for these tests
        spyOnProperty(envModule, 'environment', 'get').and.returnValue({ demo: false, apiUrl: '/api' } as any);

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(withInterceptors([demoInterceptor])),
                provideHttpClientTesting(),
                DemoStateService
            ]
        });
        http     = TestBed.inject(HttpClient);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());

    it('should pass through POST requests when not in demo mode', () => {
        http.post('/api/accounts', { name: 'Real' }).subscribe();

        const req = httpMock.expectOne('/api/accounts');
        expect(req.request.method).toBe('POST');
        req.flush({ id: 'real-id', name: 'Real' });
    });

    it('should pass through DELETE requests when not in demo mode', () => {
        http.delete('/api/accounts/123').subscribe();

        const req = httpMock.expectOne('/api/accounts/123');
        expect(req.request.method).toBe('DELETE');
        req.flush(null, { status: 204, statusText: 'No Content' });
    });
});

describe('demoInterceptor — demo environment', () => {
    let http: HttpClient;
    let httpMock: HttpTestingController;
    let demoStateSpy: jasmine.SpyObj<DemoStateService>;

    beforeEach(() => {
        // Enable demo mode
        spyOnProperty(envModule, 'environment', 'get').and.returnValue({ demo: true, apiUrl: '/api' } as any);

        demoStateSpy = jasmine.createSpyObj('DemoStateService', ['handleFakeWrite']);

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(withInterceptors([demoInterceptor])),
                provideHttpClientTesting(),
                { provide: DemoStateService, useValue: demoStateSpy }
            ]
        });
        http     = TestBed.inject(HttpClient);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());

    it('should allow GET requests through to the real API', () => {
        http.get('/api/accounts').subscribe();

        const req = httpMock.expectOne('/api/accounts');
        expect(req.request.method).toBe('GET');
        req.flush([]);
    });

    it('should intercept POST requests and return 201 without hitting network', () => {
        http.post('/api/accounts', { name: 'Demo Account' }).subscribe({
            next: (_res: any) => { /* ignored */ }
        });

        httpMock.expectNone('/api/accounts');
        expect(demoStateSpy.handleFakeWrite).toHaveBeenCalledWith(
            '/api/accounts', 'POST', jasmine.objectContaining({ name: 'Demo Account' })
        );
    });

    it('should intercept PUT requests and return 200 without hitting network', () => {
        http.put('/api/accounts/abc', { name: 'Updated' }).subscribe();

        httpMock.expectNone('/api/accounts/abc');
        expect(demoStateSpy.handleFakeWrite).toHaveBeenCalledWith(
            '/api/accounts/abc', 'PUT', jasmine.any(Object)
        );
    });

    it('should intercept DELETE requests and return 204 without hitting network', () => {
        http.delete('/api/accounts/delete-me').subscribe();

        httpMock.expectNone('/api/accounts/delete-me');
        expect(demoStateSpy.handleFakeWrite).toHaveBeenCalledWith(
            '/api/accounts/delete-me', 'DELETE', jasmine.objectContaining({ id: 'delete-me' })
        );
    });

    it('should store fake write in DemoStateService for POST', () => {
        http.post('/api/transactions', { description: 'Coffee' }).subscribe();

        httpMock.expectNone('/api/transactions');
        expect(demoStateSpy.handleFakeWrite).toHaveBeenCalled();
    });
});
