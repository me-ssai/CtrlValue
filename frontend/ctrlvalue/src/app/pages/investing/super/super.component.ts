import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../models/api.models';
import { AccountFormComponent } from '../../accounts/account-form/account-form.component';
import { ValuationsComponent } from '../../valuations/valuations.component';

@Component({
    selector: 'app-super',
    standalone: true,
    imports: [
        CommonModule, RouterModule, CurrencyPipe,
        MatTableModule, MatIconModule, MatButtonModule, MatChipsModule,
        MatTooltipModule, MatTabsModule, MatDialogModule, MatCardModule,
        ValuationsComponent
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>elderly</mat-icon> Superannuation</h1>
                <button mat-raised-button color="primary" (click)="addFund()">
                    <mat-icon>add</mat-icon>
                    Add Fund
                </button>
            </div>

            <mat-tab-group animationDuration="200ms">
                <mat-tab label="Overview">
                    <ng-template matTabContent>
                        <div class="pt-3">
                            <!-- Summary card -->
                            <div class="d-flex gap-3 mb-4 flex-wrap">
                                <mat-card class="summary-card">
                                    <mat-card-content>
                                        <div class="summary-label">Total Super Balance</div>
                                        <div class="summary-value">{{ totalBalance | currency:'AUD' }}</div>
                                    </mat-card-content>
                                </mat-card>
                                <mat-card class="summary-card">
                                    <mat-card-content>
                                        <div class="summary-label">Funds</div>
                                        <div class="summary-value">{{ dataSource.data.length }}</div>
                                    </mat-card-content>
                                </mat-card>
                            </div>

                            <div class="table-container">
                                <table mat-table [dataSource]="dataSource" class="w-100">
                                    <ng-container matColumnDef="name">
                                        <th mat-header-cell *matHeaderCellDef>Fund</th>
                                        <td mat-cell *matCellDef="let row">
                                            <a [routerLink]="['/accounts', row.id]" class="link-primary fw-semibold">{{ row.name }}</a>
                                        </td>
                                    </ng-container>
                                    <ng-container matColumnDef="currency">
                                        <th mat-header-cell *matHeaderCellDef>Currency</th>
                                        <td mat-cell *matCellDef="let row">{{ row.currency }}</td>
                                    </ng-container>
                                    <ng-container matColumnDef="currentBalance">
                                        <th mat-header-cell *matHeaderCellDef>Balance</th>
                                        <td mat-cell *matCellDef="let row" class="fw-semibold">
                                            {{ row.currentBalance | currency:row.currency }}
                                        </td>
                                    </ng-container>
                                    <ng-container matColumnDef="notes">
                                        <th mat-header-cell *matHeaderCellDef>Notes</th>
                                        <td mat-cell *matCellDef="let row" class="text-muted">{{ row.notes || '—' }}</td>
                                    </ng-container>
                                    <ng-container matColumnDef="actions">
                                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                                        <td mat-cell *matCellDef="let row">
                                            <a mat-icon-button [routerLink]="['/accounts', row.id]" matTooltip="View account & transactions">
                                                <mat-icon>open_in_new</mat-icon>
                                            </a>
                                        </td>
                                    </ng-container>
                                    <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                                    <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
                                    <tr class="mat-row" *matNoDataRow>
                                        <td class="mat-cell empty-state" colspan="5">
                                            No superannuation funds found. Click "Add Fund" to get started.
                                        </td>
                                    </tr>
                                </table>
                            </div>
                        </div>
                    </ng-template>
                </mat-tab>

                <mat-tab label="Balance History">
                    <ng-template matTabContent>
                        <app-valuations></app-valuations>
                    </ng-template>
                </mat-tab>
            </mat-tab-group>
        </div>
    `,
    styles: [`
        .summary-card {
            min-width: 160px;
        }
        .summary-label {
            font-size: 0.75rem;
            color: var(--text-secondary, #666);
            text-transform: uppercase;
            letter-spacing: 0.05em;
            margin-bottom: 4px;
        }
        .summary-value {
            font-size: 1.5rem;
            font-weight: 600;
        }
    `]
})
export class SuperComponent implements OnInit {
    private financeService = inject(FinanceService);
    private dialog = inject(MatDialog);

    displayedColumns = ['name', 'currency', 'currentBalance', 'notes', 'actions'];
    dataSource = new MatTableDataSource<AccountDto>();
    totalBalance = 0;

    ngOnInit(): void {
        this.loadAccounts();
    }

    loadAccounts(): void {
        this.financeService.getAccounts().subscribe({
            next: (accounts) => {
                const superAccounts = accounts.filter(a => a.assetClass === 'SUPER');
                this.dataSource.data = superAccounts;
                this.totalBalance = superAccounts.reduce((sum, a) => sum + (a.currentBalance ?? 0), 0);
            },
            error: (err) => console.error('Error loading super accounts:', err)
        });
    }

    addFund(): void {
        const dialogRef = this.dialog.open(AccountFormComponent, { width: '560px', maxWidth: '95vw' });
        dialogRef.afterClosed().subscribe(result => {
            if (result) this.loadAccounts();
        });
    }
}
