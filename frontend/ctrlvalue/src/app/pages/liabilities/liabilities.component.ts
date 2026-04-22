import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FinanceService } from '../../services/finance.service';
import { Liability } from '../../models/api.models';
import { LiabilityFormComponent } from './liability-form/liability-form.component';

@Component({
    selector: 'app-liabilities',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatIconModule,
        MatButtonModule,
        MatCardModule,
        MatTooltipModule,
        MatSnackBarModule,
        MatDialogModule
    ],
    templateUrl: './liabilities.component.html',
    styleUrl: './liabilities.component.scss'
})
export class LiabilitiesComponent implements OnInit {
    liabilities: Liability[] = [];
    loading = false;
    error: string | null = null;

    constructor(
        private financeService: FinanceService,
        private snackBar: MatSnackBar,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadLiabilities();
    }

    loadLiabilities(): void {
        this.loading = true;
        this.error = null;
        this.financeService.getLiabilities().subscribe({
            next: (data) => {
                this.liabilities = data;
                this.loading = false;
            },
            error: (err) => {
                this.error = 'Failed to load liabilities';
                this.loading = false;
                console.error(err);
            }
        });
    }

    getTotalAmount(): number {
        return this.liabilities.reduce((sum, liability) => sum + (liability.outstandingAmount || 0), 0);
    }

    createLiability(): void {
        const ref = this.dialog.open(LiabilityFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: {}
        });
        ref.afterClosed().subscribe(result => {
            if (result) this.loadLiabilities();
        });
    }

    editLiability(id: string): void {
        const ref = this.dialog.open(LiabilityFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: { liabilityId: id }
        });
        ref.afterClosed().subscribe(result => {
            if (result) this.loadLiabilities();
        });
    }

    deleteLiability(id: string, name: string): void {
        if (!confirm(`Are you sure you want to delete "${name}"?`)) return;
        this.financeService.deleteLiability(id).subscribe({
            next: () => {
                this.snackBar.open('Liability deleted', 'Close', { duration: 3000 });
                this.loadLiabilities();
            },
            error: (err) => {
                this.error = 'Failed to delete liability';
                console.error(err);
            }
        });
    }
}
