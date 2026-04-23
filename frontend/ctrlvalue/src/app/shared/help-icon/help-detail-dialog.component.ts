import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
    selector: 'app-help-detail-dialog',
    standalone: true,
    imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
    template: `
        <div class="help-dialog">
            <div class="help-dialog-header">
                <mat-icon class="help-dialog-icon">help_outline</mat-icon>
                <h2 mat-dialog-title>{{ data.title }}</h2>
            </div>
            <mat-dialog-content>
                <p class="help-body">{{ data.body }}</p>
            </mat-dialog-content>
            <mat-dialog-actions align="end">
                <button mat-button mat-dialog-close>Got it</button>
            </mat-dialog-actions>
        </div>
    `,
    styles: [`
        .help-dialog { padding: 8px; }
        .help-dialog-header {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 8px 24px 0;
        }
        .help-dialog-icon { color: var(--color-accent-primary); font-size: 24px; width: 24px; height: 24px; }
        h2 { margin: 0; font-size: 18px; }
        .help-body { line-height: 1.6; color: var(--color-text-secondary); white-space: pre-wrap; }
    `]
})
export class HelpDetailDialogComponent {    data = inject<{
    title: string;
    body: string;
}>(MAT_DIALOG_DATA);

}
