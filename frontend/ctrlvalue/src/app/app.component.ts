import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Router } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { ThemeService } from './services/theme.service';
import { AuthService } from './services/auth.service';
import { IdleTimeoutService } from './services/idle-timeout.service';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [RouterOutlet, MatSnackBarModule],
    template: `<router-outlet></router-outlet>`
})
export class AppComponent implements OnInit, OnDestroy {
    private themeService = inject(ThemeService);
    private authService = inject(AuthService);
    private idleService = inject(IdleTimeoutService);
    private router = inject(Router);
    private snackBar = inject(MatSnackBar);

    private subs = new Subscription();

    ngOnInit(): void {
        if (this.authService.isAuthenticated) {
            this.startIdleTracking();
        }

        // Restart idle tracking after each login
        this.subs.add(
            this.authService.currentUser$.subscribe(user => {
                if (user) { this.startIdleTracking(); }
                else      { this.idleService.stop(); }
            })
        );
    }

    private startIdleTracking(): void {
        this.idleService.stop();
        this.idleService.start();

        this.subs.add(this.idleService.warn$.subscribe(() => {
            this.snackBar.open(
                'You will be logged out in 5 minutes due to inactivity.',
                'Stay logged in',
                { duration: 5 * 60 * 1000 }
            ).onAction().subscribe(() => this.idleService.reset());
        }));

        this.subs.add(this.idleService.timeout$.subscribe(() => {
            this.snackBar.dismiss();
            this.authService.logout();
            this.router.navigate(['/login']);
        }));
    }

    ngOnDestroy(): void {
        this.subs.unsubscribe();
        this.idleService.stop();
    }
}
