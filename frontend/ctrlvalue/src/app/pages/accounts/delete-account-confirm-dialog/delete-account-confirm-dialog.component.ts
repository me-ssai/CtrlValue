import { Component, Inject, OnInit } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FinanceService } from '../../../services/finance.service';
import { AccountDeletionImpactDto } from '../../../services/api.generated';
import { firstValueFrom } from 'rxjs';

@Component({
    selector: 'app-delete-account-confirm-dialog',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatProgressSpinnerModule,
    ],
    templateUrl: './delete-account-confirm-dialog.component.html',
    styleUrls: ['./delete-account-confirm-dialog.component.scss']
})
export class DeleteAccountConfirmDialogComponent implements OnInit {
    loading = true;
    error: string | null = null;
    impact: AccountDeletionImpactDto | null = null;

    constructor(
        public dialogRef: MatDialogRef<DeleteAccountConfirmDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { accountId: string; accountName: string },
        private finance: FinanceService
    ) { }

    get deleteDisabled(): boolean {
        return this.loading || !!this.error || !this.impact;
    }

    async ngOnInit(): Promise<void> {
        try {
            this.impact = await firstValueFrom(
                this.finance.getAccountDeletionImpact(this.data.accountId)
            );
        } catch (err) {
            this.error = 'Failed to load deletion impact summary. Please try again.';
            console.error(err);
        } finally {
            this.loading = false;
        }
    }

    confirm(): void {
        if (this.deleteDisabled) return;
        this.dialogRef.close(true);
    }
}