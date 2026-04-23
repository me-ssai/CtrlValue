import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../models/api.models';
import { AccountFormComponent } from '../../accounts/account-form/account-form.component';

const OTHER_ASSET_CLASSES = new Set(['BUSINESS', 'VEHICLE', 'OTHER']);

@Component({
    selector: 'app-other-investments',
    standalone: true,
    imports: [CommonModule, RouterModule, MatTableModule, MatIconModule, MatButtonModule, MatChipsModule, MatTooltipModule, MatDialogModule, CurrencyPipe],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>workspaces</mat-icon> Other Investments</h1>
                <button mat-raised-button color="primary" (click)="addAccount()">
                    <mat-icon>add</mat-icon>
                    Add Account
                </button>
            </div>
            <p class="text-muted mb-4">Business interests, vehicles, and other investment accounts.</p>
            <div class="table-container">
                <table mat-table [dataSource]="dataSource" class="w-100">
                    <ng-container matColumnDef="name">
                        <th mat-header-cell *matHeaderCellDef>Account</th>
                        <td mat-cell *matCellDef="let row">
                            <a [routerLink]="['/accounts', row.id]" class="link-primary fw-semibold">{{ row.name }}</a>
                        </td>
                    </ng-container>
                    <ng-container matColumnDef="assetClass">
                        <th mat-header-cell *matHeaderCellDef>Type</th>
                        <td mat-cell *matCellDef="let row">
                            <mat-chip-set><mat-chip>{{ row.assetClass }}</mat-chip></mat-chip-set>
                        </td>
                    </ng-container>
                    <ng-container matColumnDef="currency">
                        <th mat-header-cell *matHeaderCellDef>Currency</th>
                        <td mat-cell *matCellDef="let row">{{ row.currency }}</td>
                    </ng-container>
                    <ng-container matColumnDef="currentBalance">
                        <th mat-header-cell *matHeaderCellDef>Current Value</th>
                        <td mat-cell *matCellDef="let row" class="fw-semibold">
                            {{ row.currentBalance | currency:row.currency }}
                        </td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef>Actions</th>
                        <td mat-cell *matCellDef="let row">
                            <a mat-icon-button [routerLink]="['/accounts', row.id]" matTooltip="View account">
                                <mat-icon>open_in_new</mat-icon>
                            </a>
                        </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
                    <tr class="mat-row" *matNoDataRow>
                        <td class="mat-cell empty-state" colspan="4">No other investment accounts found.</td>
                    </tr>
                </table>
            </div>
        </div>
    `
})
export class OtherInvestmentsComponent implements OnInit {
    private financeService = inject(FinanceService);
    private dialog = inject(MatDialog);

    displayedColumns = ['name', 'assetClass', 'currency', 'currentBalance', 'actions'];
    dataSource = new MatTableDataSource<AccountDto>();

    ngOnInit(): void {
        this.loadAccounts();
    }

    loadAccounts(): void {
        this.financeService.getAccounts().subscribe({
            next: (accounts) => {
                this.dataSource.data = accounts.filter(a => OTHER_ASSET_CLASSES.has(a.assetClass ?? ''));
            },
            error: (err) => console.error('Error loading accounts:', err)
        });
    }

    addAccount(): void {
        const dialogRef = this.dialog.open(AccountFormComponent, { width: '500px' });
        dialogRef.afterClosed().subscribe(result => {
            if (result) this.loadAccounts();
        });
    }
}
