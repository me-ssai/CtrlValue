import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { of } from 'rxjs';
import { environment } from '../../environments/environment';
import { DemoStateService } from '../services/demo-state.service';

/**
 * HTTP interceptor that runs only in demo mode (environment.demo === true).
 *
 * GET / HEAD requests pass through to the backend unchanged so the demo
 * frontend reads real seeded data.
 *
 * POST / PUT / PATCH / DELETE requests are intercepted before hitting the
 * network. A fake success response is returned and the change is stored in
 * the in-memory session overlay inside DemoStateService.
 *
 * The backend also rejects all writes (BlockDemoWritesFilter) as a second
 * line of defence, but the frontend intercept ensures visitors never see
 * an error.
 */
export const demoInterceptor: HttpInterceptorFn = (req, next) => {
    if (!environment.demo) return next(req);

    const method = req.method.toUpperCase();

    // Let reads pass through to the real API
    if (method === 'GET' || method === 'HEAD') return next(req);

    const demoState = inject(DemoStateService);
    const fakeId    = crypto.randomUUID();
    const body      = req.body as Record<string, unknown> | null;

    if (method === 'POST') {
        const fakeBody = { ...body, id: fakeId, createdAt: new Date().toISOString() };
        demoState.handleFakeWrite(req.url, 'POST', fakeBody);
        return of(new HttpResponse({ status: 201, body: fakeBody }));
    }

    if (method === 'PUT' || method === 'PATCH') {
        const fakeBody = { ...body, id: body?.['id'] ?? fakeId, updatedAt: new Date().toISOString() };
        demoState.handleFakeWrite(req.url, method, fakeBody);
        return of(new HttpResponse({ status: 200, body: fakeBody }));
    }

    if (method === 'DELETE') {
        // Extract ID from URL (last path segment)
        const segments = req.url.split('/').filter(Boolean);
        const urlId    = segments[segments.length - 1];
        demoState.handleFakeWrite(req.url, 'DELETE', { id: urlId });
        return of(new HttpResponse({ status: 204, body: null }));
    }

    return next(req);
};
