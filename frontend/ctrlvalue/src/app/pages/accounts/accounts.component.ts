import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FinanceService } from '../../services/finance.service';
import { Account } from '../../models/api.models';
import { AccountFormComponent } from './account-form/account-form.component';

@Component({
    selector: 'app-accounts',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatButtonModule,
        MatIconModule,
        MatChipsModule,
        MatSnackBarModule,
        MatDialogModule
    ],
    templateUrl: './accounts.component.html',
    styleUrl: './accounts.component.scss'
})
export class AccountsComponent implements OnInit {
    accounts: Account[] = [];
    loading = false;
    refreshingBalances = false;

    constructor(
        private financeService: FinanceService,
        private router: Router,
        private snackBar: MatSnackBar,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadAccounts();
    }

    loadAccounts(): void {
        this.loading = true;
        this.financeService.getAccounts().subscribe({
            next: (data) => {
                this.accounts = data;
                this.loading = false;
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open('Failed to load accounts', 'Close', { duration: 5000 });
            }
        });
    }

    get assetAccounts(): Account[] {
        return this.accounts.filter(a => a.accountType === 'ASSET');
    }

    get liabilityAccounts(): Account[] {
        return this.accounts.filter(a => a.accountType === 'LIABILITY');
    }

    openAccount(id: string): void {
        this.router.navigate(['/accounts', id]);
    }

    refreshBalances(): void {
        this.refreshingBalances = true;
        this.financeService.recalculateAllBalances().subscribe({
            next: () => {
                this.refreshingBalances = false;
                this.loadAccounts();
                this.snackBar.open('Balances refreshed', 'Close', { duration: 3000 });
            },
            error: () => {
                this.refreshingBalances = false;
                this.snackBar.open('Failed to refresh balances', 'Close', { duration: 5000 });
            }
        });
    }

    createAccount(): void {
        const dialogRef = this.dialog.open(AccountFormComponent, {
            width: '680px'
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.loadAccounts();
            }
        });
    }

    formatCurrency(value: number | null | undefined): string {
        return new Intl.NumberFormat('en-AU', {
            style: 'currency',
            currency: 'AUD',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value ?? 0);
    }

    getAccountIcon(accountType: string | undefined, assetClass?: string | null): string {
        if (accountType === 'LIABILITY') return 'credit_card';
        const iconMap: Record<string, string> = {
            CASH: 'account_balance_wallet',
            STOCK: 'show_chart',
            ETF: 'pie_chart',
            PROPERTY: 'home',
            VEHICLE: 'directions_car',
            METAL: 'diamond',
            SUPER: 'savings',
            BUSINESS: 'business',
            CRYPTO: 'currency_bitcoin',
        };
        return iconMap[assetClass ?? ''] ?? 'account_balance';
    }
}
