import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../services/auth.service';

@Component({
    selector: 'app-verify-email',
    standalone: true,
    imports: [CommonModule, FormsModule, MatCardModule, MatProgressSpinnerModule, MatIconModule, MatButtonModule, MatFormFieldModule, MatInputModule],
    templateUrl: './verify-email.component.html',
    styleUrls: ['./verify-email.component.scss']
})
export class VerifyEmailComponent implements OnInit {
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private authService = inject(AuthService);

    state: 'loading' | 'expired' | 'error' = 'loading';
    errorMessage = '';

    resendEmail = '';
    resendLoading = false;
    resendSent = false;
    resendError = '';

    ngOnInit(): void {
        const token = this.route.snapshot.queryParamMap.get('token');
        if (!token) {
            this.state = 'error';
            this.errorMessage = 'Invalid verification link.';
            return;
        }

        this.authService.verifyEmail(token).subscribe({
            next: () => this.router.navigate(['/login'], { queryParams: { verified: 'true' } }),
            error: (err) => {
                const msg: string = err.error?.message ?? '';
                if (msg.toLowerCase().includes('expired')) {
                    this.state = 'expired';
                    this.errorMessage = msg;
                } else {
                    this.state = 'error';
                    this.errorMessage = msg || 'Verification failed. The link may be invalid.';
                }
            }
        });
    }

    resendVerification(): void {
        if (!this.resendEmail) return;
        this.resendLoading = true;
        this.resendError = '';
        this.authService.resendVerification(this.resendEmail).subscribe({
            next: () => { this.resendLoading = false; this.resendSent = true; },
            error: (err) => {
                this.resendLoading = false;
                this.resendError = err.error?.message || 'Failed to resend. Please try again.';
            }
        });
    }

    goToLogin(): void {
        this.router.navigate(['/login']);
    }
}
