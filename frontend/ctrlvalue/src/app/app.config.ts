import { APP_INITIALIZER, ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { routes } from './app.routes';
import { Client, API_BASE_URL } from './services/api.generated';
import { environment } from '../environments/environment';
import { jwtInterceptor } from './interceptors/jwt.interceptor';
import { demoInterceptor } from './interceptors/demo.interceptor';
import { DemoStateService, DemoBootstrapDto } from './services/demo-state.service';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { provideNativeDateAdapter } from '@angular/material/core';

export const appConfig: ApplicationConfig = {
    providers: [
        provideZoneChangeDetection({ eventCoalescing: true }),
        provideRouter(routes),
        provideHttpClient(withInterceptors([jwtInterceptor, demoInterceptor])),
        provideAnimationsAsync(),
        provideNativeDateAdapter(),
        provideCharts(withDefaultRegisterables()),
        Client,
        { provide: API_BASE_URL, useValue: environment.apiUrl.replace('/api', '') },
        // Load demo baseline data before first render (no-op in non-demo builds)
        {
            provide: APP_INITIALIZER,
            useFactory: (demoState: DemoStateService, http: HttpClient) => () => {
                if (!environment.demo) return of(null);
                return http
                    .get<DemoBootstrapDto>(`${environment.apiUrl}/demo/bootstrap`)
                    .pipe(
                        tap(data => demoState.setBaseline(data)),
                        catchError(() => of(null))
                    );
            },
            deps: [DemoStateService, HttpClient],
            multi: true,
        },
    ]
};
