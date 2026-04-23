import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

@Component({
    selector: 'app-help-icon',
    standalone: true,
    imports: [CommonModule, MatIconModule, MatTooltipModule, MatButtonModule],
    template: `
        <button mat-icon-button class="help-btn"
            [matTooltip]="text"
            matTooltipShowDelay="200"
            (click)="detailed ? showDetail($event) : null"
            [class.clickable]="detailed"
            type="button"
            aria-label="Help">
            <mat-icon class="help-icon">help_outline</mat-icon>
        </button>
    `,
    styles: [`
        .help-btn {
            width: 22px;
            height: 22px;
            line-height: 22px;
            padding: 0;
            vertical-align: middle;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            opacity: 0.55;
            transition: opacity 0.15s;
        }
        .help-btn:hover { opacity: 0.9; }
        .help-icon {
            font-size: 16px;
            width: 16px;
            height: 16px;
            color: inherit;
        }
    `]
})
export class HelpIconComponent {
    private dialog = inject(MatDialog);

    /** Short tooltip shown on hover. */
    @Input() text = '';
    /** Longer title for the detail dialog (optional). */
    @Input() title = '';
    /** Longer body text for the detail dialog. */
    @Input() body = '';
    /** If true, clicking opens a detail dialog with title+body. */
    @Input() detailed = false;

    showDetail(event: Event): void {
        event.stopPropagation();
        import('./help-detail-dialog.component').then(m => {
            this.dialog.open(m.HelpDetailDialogComponent, {
                width: '440px',
                data: { title: this.title || 'Help', body: this.body || this.text }
            });
        });
    }
}
