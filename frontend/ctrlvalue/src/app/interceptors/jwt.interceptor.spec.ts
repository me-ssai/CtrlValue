import { TestBed } from '@angular/core/testing';
import {
    HttpClientTestingModule,
    HttpTestingController
} from '@angular/common/http/testing';
import {
    HTTP_INTERCEPTORS,
    HttpClient,
    provideHttpClient,
    withInterceptors
} from '@angular/common/http';
import { jwtInterceptor } from './jwt.interceptor';
import { EntityService } from '../services/entity.service';

describe('jwtInterceptor', () => {
    let httpMock: HttpTestingController;
    let http: HttpClient;
    let entityServiceSpy: jasmine.SpyObj<EntityService>;

    function setup(entityId: string | null) {
        entityServiceSpy = jasmine.createSpyObj('EntityService', [], {
            currentEntityId: entityId
        });

        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [
                provideHttpClient(withInterceptors([jwtInterceptor])),
                { provide: EntityService, useValue: entityServiceSpy }
            ]
        });

        http      = TestBed.inject(HttpClient);
        httpMock  = TestBed.inject(HttpTestingController);
    }

    afterEach(() => {
        httpMock.verify();
    });

    it('should add X-Entity-Id header when currentEntityId is set', () => {
        setup('entity-abc-123');

        http.get('/api/test').subscribe();

        const req = httpMock.expectOne('/api/test');
        expect(req.request.headers.get('X-Entity-Id')).toBe('entity-abc-123');
        req.flush({});
    });

    it('should NOT add X-Entity-Id header when currentEntityId is null', () => {
        setup(null);

        http.get('/api/test').subscribe();

        const req = httpMock.expectOne('/api/test');
        expect(req.request.headers.has('X-Entity-Id')).toBeFalse();
        req.flush({});
    });

    it('should set withCredentials: true on all requests', () => {
        setup('entity-xyz');

        http.get('/api/test').subscribe();

        const req = httpMock.expectOne('/api/test');
        expect(req.request.withCredentials).toBeTrue();
        req.flush({});
    });

    it('should set withCredentials: true even when no entity is selected', () => {
        setup(null);

        http.post('/api/auth/login', {}).subscribe();

        const req = httpMock.expectOne('/api/auth/login');
        expect(req.request.withCredentials).toBeTrue();
        req.flush({});
    });

    it('should pass the request through unmodified (excluding headers)', () => {
        setup('entity-pass');
        const body = { key: 'value' };

        http.post('/api/test', body).subscribe();

        const req = httpMock.expectOne('/api/test');
        expect(req.request.body).toEqual(body);
        req.flush({});
    });
});
