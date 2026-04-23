import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../services/auth.service';

type LoginStep = 'credentials' | 'notice';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterLink,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatIconModule,
        MatSnackBarModule
    ],
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
    private authService = inject(AuthService);
    private router = inject(Router);
    private route = inject(ActivatedRoute);
    private snackBar = inject(MatSnackBar);

    step: LoginStep = 'credentials';

    email = '';
    password = '';
    loading = false;

    constructor() {
        if (this.authService.isAuthenticated) {
            this.router.navigate(['/dashboard']);
        }
    }

    ngOnInit(): void {
        if (this.route.snapshot.queryParamMap.get('verified') === 'true') {
            this.snackBar.open('Email verified successfully! You can now log in.', 'Close', { duration: 6000 });
        }
    }

    onSubmitCredentials(): void {
        if (!this.email || !this.password) {
            this.snackBar.open('Please fill in all fields', 'Close', { duration: 3000 });
            return;
        }

        this.loading = true;

        this.authService.login({ email: this.email, password: this.password }).subscribe({
            next: () => {
                this.loading = false;
                this.step = 'notice';
            },
            error: (err) => {
                this.loading = false;
                const message = err.status === 429
                    ? 'Too many login attempts. Please wait 15 minutes before trying again.'
                    : err.error?.message || err.error?.error || 'Login failed. Please try again.';
                this.snackBar.open(message, 'Close', { duration: 8000 });
            }
        });
    }

    acknowledgeNotice(): void {
        this.router.navigate(['/dashboard']);
    }
}
