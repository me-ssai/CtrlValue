import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { EntityService } from '../services/entity.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
    const entityId = inject(EntityService).currentEntityId;

    const headers: Record<string, string> = {};

    if (entityId) {
        headers['X-Entity-Id'] = entityId;
    }

    req = req.clone({
        withCredentials: true,  // sends httpOnly auth cookies automatically
        setHeaders: headers
    });

    return next(req);
};
