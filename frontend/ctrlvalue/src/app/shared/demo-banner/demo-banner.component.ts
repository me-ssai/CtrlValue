import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DemoStateService } from '../../services/demo-state.service';

@Component({
    selector: 'app-demo-banner',
    standalone: true,
    imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
    template: `
        <div class="demo-banner" role="banner" aria-label="Demo mode notice">
            <div class="demo-banner-content">
                <mat-icon class="demo-icon">science</mat-icon>
                <span class="demo-text">
                    <strong>Demo Mode</strong> — This is a demonstration workspace with fictional data.
                    Your changes are temporary and will disappear when you close this tab.
                </span>
            </div>
            <div class="demo-actions">
                <button
                    mat-stroked-button
                    class="reset-btn"
                    [disabled]="resetting"
                    (click)="resetDemo()">
                    <mat-spinner *ngIf="resetting" diameter="16" class="btn-spinner"></mat-spinner>
                    <mat-icon *ngIf="!resetting">refresh</mat-icon>
                    Reset Demo
                </button>

                <!-- Email capture -->
                <div class="email-capture">
                    <div *ngIf="!submitted" class="email-form">
                        <input
                            type="email"
                            placeholder="your@email.com"
                            [(ngModel)]="email"
                            class="email-input"
                            (keydown.enter)="submitEmail()" />
                        <button class="email-submit-btn" (click)="submitEmail()">
                            Get Early Access
                        </button>
                    </div>
                    <div *ngIf="submitted" class="email-thanks">
                        <span class="thanks-check">✓</span>
                        <span class="thanks-text">You're on the list!</span>
                    </div>
                </div>
            </div>
        </div>
    `,
    styles: [`
        .demo-banner {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
            padding: 8px 20px;
            background: #92400e;
            color: #fef3c7;
            font-size: 13px;
            flex-wrap: wrap;
        }
        .demo-banner-content {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        .demo-icon {
            font-size: 18px;
            height: 18px;
            width: 18px;
            flex-shrink: 0;
        }
        .demo-text strong {
            font-weight: 600;
        }
        .demo-actions {
            display: flex;
            align-items: center;
            gap: 8px;
            flex-shrink: 0;
        }
        .reset-btn {
            color: #fef3c7;
            border-color: rgba(254, 243, 199, 0.5);
            font-size: 12px;
            height: 30px;
            line-height: 30px;
            display: flex;
            align-items: center;
            gap: 4px;
        }
        .reset-btn mat-icon {
            font-size: 16px;
            height: 16px;
            width: 16px;
        }
        .btn-spinner {
            display: inline-flex;
        }

        /* Email capture */
        .email-capture {
            display: flex;
            align-items: center;
        }
        .email-form {
            display: flex;
            align-items: center;
            gap: 0;
            border: 1px solid rgba(254, 243, 199, 0.5);
            border-radius: 3px;
            overflow: hidden;
        }
        .email-input {
            background: rgba(255,255,255,0.12);
            border: none;
            outline: none;
            padding: 0 12px;
            height: 30px;
            font-size: 12px;
            color: #fef3c7;
            width: 180px;
        }
        .email-input::placeholder {
            color: rgba(254, 243, 199, 0.55);
        }
        .email-submit-btn {
            background: #fef3c7;
            color: #92400e;
            border: none;
            padding: 0 14px;
            height: 30px;
            font-size: 12px;
            font-weight: 600;
            cursor: pointer;
            white-space: nowrap;
            transition: background 0.15s;
        }
        .email-submit-btn:hover {
            background: #fde68a;
        }
        .email-thanks {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 12px;
            color: #fef3c7;
        }
        .thanks-check {
            display: flex;
            align-items: center;
            justify-content: center;
            width: 20px;
            height: 20px;
            border-radius: 50%;
            background: rgba(254, 243, 199, 0.2);
            font-size: 11px;
        }
    `]
})
export class DemoBannerComponent {
    private demoState = inject(DemoStateService);
    resetting = false;
    email = '';
    submitted = false;

    resetDemo(): void {
        this.resetting = true;
        this.demoState.reset().subscribe({
            next: () => { this.resetting = false; window.location.reload(); },
            error: () => { this.resetting = false; }
        });
    }

    submitEmail(): void {
        if (!this.email.includes('@')) return;
        this.submitted = true;
    }
}
