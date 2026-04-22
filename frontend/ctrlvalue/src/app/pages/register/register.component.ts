import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-register',
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
    templateUrl: './register.component.html',
    styleUrls: ['./register.component.scss']
})
export class RegisterComponent {
    firstName = '';
    lastName = '';
    email = '';
    password = '';
    confirmPassword = '';
    loading = false;

    constructor(
        private authService: AuthService,
        private router: Router,
        private snackBar: MatSnackBar
    ) {
        if (this.authService.isAuthenticated) {
            this.router.navigate(['/dashboard']);
        }
    }

    onSubmit(): void {
        if (!this.firstName || !this.lastName || !this.email || !this.password) {
            this.snackBar.open('Please fill in all fields', 'Close', { duration: 3000 });
            return;
        }

        if (this.password !== this.confirmPassword) {
            this.snackBar.open('Passwords do not match', 'Close', { duration: 3000 });
            return;
        }

        if (this.password.length < 8) {
            this.snackBar.open('Password must be at least 8 characters', 'Close', { duration: 3000 });
            return;
        }

        this.loading = true;

        this.authService.register({
            email: this.email,
            password: this.password,
            firstName: this.firstName,
            lastName: this.lastName
        }).subscribe({
            next: (response) => {
                this.loading = false;
                if (response.requiresEmailVerification) {
                    this.snackBar.open('Registration successful. Please check your email to verify your account.', 'Close', { duration: 5000 });
                    this.router.navigate(['/login']);
                } else {
                    this.router.navigate(['/dashboard']);
                }
            },
            error: (err) => {
                this.loading = false;
                const errorMessage = err.status === 429
                    ? 'Too many attempts. Please wait 15 minutes before trying again.'
                    : err.error?.error || err.error?.message || 'Registration failed. Please try again.';
                this.snackBar.open(errorMessage, 'Close', { duration: 8000 });
            }
        });
    }
}
