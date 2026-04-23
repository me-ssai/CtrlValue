import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SelectionModel } from '@angular/cdk/collections';
import { ViewChild, AfterViewInit } from '@angular/core';

import { FinanceService } from '../../../services/finance.service';
import { IntelligenceService } from '../../../services/intelligence.service';
import { AccountKeywordRuleService } from '../../../services/account-keyword-rule.service';
import { Account, Transaction, UpdateAccountRequest, KeywordMatchType, CreateAccountKeywordRuleRequest } from '../../../models/api.models';
import { AccountDto } from '../../../services/api.generated';
import { TransactionFormComponent } from '../../transactions/transaction-form/transaction-form.component';
import { QifImportComponent } from '../../transactions/qif-import/qif-import.component';
import { OfxImportComponent } from '../../transactions/ofx-import/ofx-import.component';
import { LoanDetailFormComponent } from '../loan-detail-form/loan-detail-form.component';
import { LinkInvestmentDialogComponent } from '../../transactions/link-investment-dialog/link-investment-dialog.component';
import { QuickCategorizeDialogComponent, QuickCategorizeDialogResult } from '../../transactions/quick-categorize-dialog/quick-categorize-dialog.component';
import { DeleteAccountConfirmDialogComponent } from '../delete-account-confirm-dialog/delete-account-confirm-dialog.component';
import { AccountKeywordsComponent } from '../account-keywords/account-keywords.component';

@Component({
    selector: 'app-account-detail',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        ReactiveFormsModule,
        FormsModule,
        MatTableModule,
        MatPaginatorModule,
        MatSortModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatSnackBarModule,
        MatDialogModule,
        MatChipsModule,
        MatTooltipModule,
        MatCheckboxModule,
        MatProgressSpinnerModule,
        LoanDetailFormComponent,
        AccountKeywordsComponent
    ],
    templateUrl: './account-detail.component.html',
    styleUrl: './account-detail.component.scss'
})
export class AccountDetailComponent implements OnInit, AfterViewInit {
    private route = inject(ActivatedRoute);
    private router = inject(Router);
    private financeService = inject(FinanceService);
    private intelligenceService = inject(IntelligenceService);
    private keywordRuleService = inject(AccountKeywordRuleService);
    private fb = inject(FormBuilder);
    private snackBar = inject(MatSnackBar);
    private dialog = inject(MatDialog);

    accountId!: string;
    entityId!: string;
    account: Account | null = null;
    accountForm!: FormGroup;

    // Transactions table
    displayedColumns: string[] = ['select', 'date', 'type', 'category', 'amount', 'description', 'counterAccount', 'keyword', 'investment', 'actions'];
    dataSource = new MatTableDataSource<Transaction>();
    selection = new SelectionModel<Transaction>(true, []);
    accounts: AccountDto[] = [];
    loadingAccount = false;
    loadingTxns = false;
    savingAccount = false;
    deletingBulk = false;

    // Counter account / keyword state
    keywordInputs: Record<string, string> = {};
    addingKeyword: Record<string, boolean> = {};

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    readonly assetClasses = ['CASH', 'STOCK', 'ETF', 'METAL', 'VEHICLE', 'PROPERTY', 'SUPER', 'BUSINESS', 'CRYPTO', 'OTHER'];
    readonly liquidityClasses = ['LIQUID', 'SEMI_LIQUID', 'ILLIQUID', 'LOCKED'];

    private readonly transactionMeta: Record<string, { cssClass: string }> = {
        Income: { cssClass: 'income' },
        AssetSale: { cssClass: 'income' },
        LoanDisbursement: { cssClass: 'income' },
        CapitalDeposit: { cssClass: 'income' },
        OpeningBalance: { cssClass: 'neutral' },
        Expense: { cssClass: 'expense' },
        AssetPurchase: { cssClass: 'expense' },
        LoanRepayment: { cssClass: 'expense' },
        LoanInterestCharge: { cssClass: 'expense' },
        CapitalWithdrawal: { cssClass: 'expense' },
        Transfer: { cssClass: 'transfer' }
    };

    bannerDismissed = false;

    get uncategorizedCount(): number {
        return this.dataSource.data.filter(t => !t.categoryId).length;
    }

    ngOnInit(): void {
        this.accountId = this.route.snapshot.paramMap.get('id')!;
        this.loadAccount();
        this.loadTransactions();
        this.financeService.getAccounts().subscribe({ next: (a) => this.accounts = a });
    }

    ngAfterViewInit(): void {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    loadAccount(): void {
        this.loadingAccount = true;
        this.financeService.getAccount(this.accountId).subscribe({
            next: (acc) => {
                this.account = acc;
                this.entityId = (acc as any).entityId ?? '';
                this.buildForm(acc);
                this.loadingAccount = false;
            },
            error: () => {
                this.loadingAccount = false;
                this.snackBar.open('Failed to load account', 'Close', { duration: 5000 });
            }
        });
    }

    private buildForm(acc: Account): void {
        this.accountForm = this.fb.group({
            name: [acc.name, [Validators.required, Validators.maxLength(256)]],
            assetClass: [acc.assetClass ?? ''],
            liquidityClass: [acc.liquidityClass ?? ''],
            currency: [acc.currency, Validators.required],
            institution: [acc.institution ?? ''],
            accountNumber: [acc.accountNumber ?? ''],
            notes: [acc.notes ?? ''],
            isActive: [acc.isActive],
            creditLimit: [acc.creditLimit ?? null],
            startingBalance: [acc.startingBalance ?? 0],
            startingBalanceDate: [acc.startingBalanceDate ? new Date(acc.startingBalanceDate) : new Date()],
            isOffsetAccount: [acc.isOffsetAccount ?? false],
            openedAt: [acc.openedAt ? new Date(acc.openedAt) : null],
            closedAt: [acc.closedAt ? new Date(acc.closedAt) : null],
            externalId: [acc.externalId ?? '']
        });
    }

    saveAccount(): void {
        if (!this.accountForm || this.accountForm.invalid) {
            this.accountForm?.markAllAsTouched();
            return;
        }
        this.savingAccount = true;
        const v = this.accountForm.value;
        const request: UpdateAccountRequest = {
            name: v.name,
            assetClass: v.assetClass || undefined,
            liquidityClass: v.liquidityClass || undefined,
            currency: v.currency,
            institution: v.institution || undefined,
            accountNumber: v.accountNumber || undefined,
            notes: v.notes || undefined,
            isActive: v.isActive,
            creditLimit: v.creditLimit || undefined,
            startingBalance: v.startingBalance ?? 0,
            startingBalanceDate: v.startingBalanceDate ?? (v.startingBalanceDate as Date).toISOString(),
            isOffsetAccount: v.isOffsetAccount ?? false,
            openedAt: v.openedAt || undefined,
            closedAt: v.closedAt || undefined,
            externalId: v.externalId || undefined
        };
        this.financeService.updateAccount(this.accountId, request).subscribe({
            next: (acc) => {
                this.account = acc;
                this.savingAccount = false;
                this.snackBar.open('Account saved', 'Close', { duration: 3000 });
            },
            error: (err) => {
                this.savingAccount = false;
                this.snackBar.open(err.error?.message || 'Failed to save account', 'Close', { duration: 5000 });
            }
        });
    }

    loadTransactions(): void {
        this.loadingTxns = true;
        this.financeService.getTransactionsByAccount(this.accountId).subscribe({
            next: (txns) => {
                this.dataSource.data = txns;
                this.selection.clear();
                this.loadingTxns = false;
            },
            error: () => {
                this.loadingTxns = false;
                this.snackBar.open('Failed to load transactions', 'Close', { duration: 5000 });
            }
        });
    }

    updateTransactionCounterAccount(tx: Transaction, accountId: string): void {
        this.financeService.updateTransaction(tx.id!, { counterAccountId: accountId || undefined }).subscribe({
            next: (updated) => {
                tx.counterAccountId = updated.counterAccountId;
                tx.counterAccountName = updated.counterAccountName;
            },
            error: () => this.snackBar.open('Failed to update counter account', 'Close', { duration: 3000 })
        });
    }

    addKeywordForTransaction(tx: Transaction, keyword: string): void {
        if (!tx.counterAccountId) {
            this.snackBar.open('Select a counter account first', 'Close', { duration: 3000 });
            return;
        }
        const trimmed = keyword?.trim();
        if (!trimmed) return;

        this.addingKeyword[tx.id!] = true;

        const request: CreateAccountKeywordRuleRequest = {
            accountId: tx.counterAccountId,
            keyword: trimmed,
            matchType: KeywordMatchType.Contains,
            isCaseSensitive: false
        };

        this.keywordRuleService.create(request).subscribe({
            next: () => {
                this.addingKeyword[tx.id!] = false;
                this.keywordInputs[tx.id!] = '';

                // Apply to all unlinked transactions in this account whose description matches
                const lowerKw = trimmed.toLowerCase();
                const matches = this.dataSource.data.filter(t =>
                    t.id !== tx.id &&
                    !t.counterAccountId &&
                    (t.description ?? '').toLowerCase().includes(lowerKw)
                );
                matches.forEach(t => this.updateTransactionCounterAccount(t, tx.counterAccountId!));

                const msg = matches.length > 0
                    ? `Keyword saved. Applied to ${matches.length} other transaction${matches.length > 1 ? 's' : ''}.`
                    : 'Keyword saved.';
                this.snackBar.open(msg, 'Close', { duration: 4000 });
            },
            error: () => {
                this.addingKeyword[tx.id!] = false;
                this.snackBar.open('Failed to save keyword', 'Close', { duration: 3000 });
            }
        });
    }

    createTransaction(): void {
        const ref = this.dialog.open(TransactionFormComponent, {
            width: '720px',
            maxWidth: '95vw',
            data: { preselectedAccountId: this.accountId }
        });
        ref.afterClosed().subscribe(result => { if (result) this.loadTransactions(); });
    }

    editTransaction(id: string): void {
        const ref = this.dialog.open(TransactionFormComponent, {
            width: '720px',
            maxWidth: '95vw',
            data: { transactionId: id }
        });
        ref.afterClosed().subscribe(result => { if (result) this.loadTransactions(); });
    }

    deleteTransaction(id: string): void {
        if (!confirm('Delete this transaction?')) return;
        this.financeService.deleteTransaction(id).subscribe({
            next: () => {
                this.snackBar.open('Transaction deleted', 'Close', { duration: 3000 });
                this.loadTransactions();
            },
            error: () => this.snackBar.open('Failed to delete transaction', 'Close', { duration: 5000 })
        });
    }

    bulkDeleteTransactions(): void {
        const ids = this.selection.selected.map(x => x.id) as string[];
        if (!ids.length) {
            this.snackBar.open('Select at least one transaction', 'Close', { duration: 3000 });
            return;
        }
        if (!confirm(`Delete ${ids.length} transaction${ids.length > 1 ? 's' : ''}?`)) return;
        this.deletingBulk = true;
        this.financeService.bulkDelete(ids).subscribe({
            next: () => {
                this.deletingBulk = false;
                this.snackBar.open(`${ids.length} transaction${ids.length > 1 ? 's' : ''} deleted`, 'Close', { duration: 4000 });
                this.loadTransactions();
            },
            error: () => {
                this.deletingBulk = false;
                this.snackBar.open('Bulk delete failed', 'Close', { duration: 5000 });
            }
        });
    }

    openQuickCategorize(tx: Transaction): void {
        const ref = this.dialog.open(QuickCategorizeDialogComponent, {
            width: '480px',
            maxWidth: '96vw',
            data: { transaction: tx }
        });
        ref.afterClosed().subscribe((result: QuickCategorizeDialogResult | undefined) => {
            if (result !== undefined) {
                this.loadTransactions();
                if (result.categorizedCount > 0) {
                    this.snackBar.open(
                        `${result.categorizedCount} additional transaction(s) auto-categorized`,
                        'Close',
                        { duration: 5000 }
                    );
                }
            }
        });
    }

    linkInvestment(transaction: any): void {
        const ref = this.dialog.open(LinkInvestmentDialogComponent, {
            width: '620px',
            maxWidth: '95vw',
            data: { transaction }
        });
        ref.afterClosed().subscribe(result => { if (result) this.loadTransactions(); });
    }

    openQifImport(): void {
        const ref = this.dialog.open(QifImportComponent, {
            width: '1400px', maxWidth: '96vw', maxHeight: '92vh',
            disableClose: true, panelClass: 'qif-import-dialog',
            data: { preselectedAccountId: this.accountId }
        });
        ref.afterClosed().subscribe((committed: boolean) => { if (committed) this.loadTransactions(); });
    }

    openOfxImport(): void {
        const ref = this.dialog.open(OfxImportComponent, {
            width: '1400px', maxWidth: '96vw', maxHeight: '92vh',
            disableClose: true, panelClass: 'ofx-import-dialog',
            data: { preselectedAccountId: this.accountId }
        });
        ref.afterClosed().subscribe((committed: boolean) => { if (committed) this.loadTransactions(); });
    }

    isAllSelected(): boolean {
        return this.dataSource.data.length > 0 && this.selection.selected.length === this.dataSource.data.length;
    }

    toggleAllRows(): void {
        if (this.isAllSelected()) {
            this.selection.clear();
        } else {
            this.selection.select(...this.dataSource.data);
        }
    }

    get selectedCount(): number { return this.selection.selected.length; }

    getTransactionCssClass(type?: string): string { return this.transactionMeta[type ?? '']?.cssClass ?? 'neutral'; }

    getTransactionSign(type?: string): string {
        const pos = ['Income', 'AssetSale', 'LoanDisbursement', 'CapitalDeposit'];
        const neg = ['Expense', 'AssetPurchase', 'LoanRepayment', 'CapitalWithdrawal'];
        if (!type) return '';
        if (pos.includes(type)) return '+';
        if (neg.includes(type)) return '-';
        return '';
    }

    formatCurrency(value: number | null | undefined): string {
        return new Intl.NumberFormat('en-AU', {
            style: 'currency', currency: 'AUD',
            minimumFractionDigits: 0, maximumFractionDigits: 0
        }).format(value ?? 0);
    }

    deleteAccount(): void {
        const dialogRef = this.dialog.open(DeleteAccountConfirmDialogComponent, {
            width: '480px',
            data: { accountId: this.account!.id!, accountName: this.account!.name! }
        });
        dialogRef.afterClosed().subscribe(confirmed => {
            if (confirmed) {
                this.financeService.deleteAccount(this.account!.id!).subscribe({
                    next: () => {
                        this.snackBar.open('Account deleted', 'Close', { duration: 4000 });
                        this.router.navigate(['/accounts']);
                    },
                    error: () => this.snackBar.open('Failed to delete account', 'Close', { duration: 5000 })
                });
            }
        });
    }

    goBack(): void { this.router.navigate(['/accounts']); }
}
