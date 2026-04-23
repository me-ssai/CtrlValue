import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { FinanceService } from '../../../services/finance.service';
import { CreateTransactionRequest, UpdateTransactionRequest, CategoryDto, AccountDto, InstrumentDto, TransactionType } from '../../../models/api.models';
import { HelpIconComponent } from '../../../shared/help-icon/help-icon.component';

export interface TransactionDialogData {
    transactionId?: string;
    preselectedAccountId?: string;
}

// Asset classes that have tradeable instruments
const INSTRUMENT_ASSET_CLASSES = new Set(['STOCK', 'ETF', 'BOND', 'CRYPTO', 'FUND', 'METAL']);

@Component({
    selector: 'app-transaction-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatButtonModule,
        MatIconModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatCheckboxModule,
        MatSnackBarModule,
        HelpIconComponent
    ],
    templateUrl: './transaction-form.component.html',
    styleUrl: './transaction-form.component.scss'
})
export class TransactionFormComponent implements OnInit, OnDestroy {
    private fb = inject(FormBuilder);
    private financeService = inject(FinanceService);
    private dialogRef = inject<MatDialogRef<TransactionFormComponent>>(MatDialogRef);
    private snackBar = inject(MatSnackBar);
    data = inject<TransactionDialogData>(MAT_DIALOG_DATA);

    transactionForm: FormGroup;
    isEditMode = false;
    transactionId: string | null = null;
    loading = false;
    categories: CategoryDto[] = [];
    accounts: AccountDto[] = [];
    instruments: InstrumentDto[] = [];
    transactionTypes = Object.values(TransactionType);
    txnTypeDisplay: Record<string, string> = {
        [TransactionType.Income]: 'Income',
        [TransactionType.Expense]: 'Expense',
        [TransactionType.AssetPurchase]: 'Asset Purchase',
        [TransactionType.AssetSale]: 'Asset Sale',
        [TransactionType.Transfer]: 'Transfer',
        [TransactionType.LoanDisbursement]: 'Loan Disbursement',
        [TransactionType.LoanRepayment]: 'Loan Repayment',
        [TransactionType.CapitalDeposit]: 'Capital Deposit',
        [TransactionType.CapitalWithdrawal]: 'Capital Withdrawal'
    };

    // Investment section visibility
    selectedAccountAssetClass: string | null = null;
    filteredInstruments: InstrumentDto[] = [];

    // Auto-calculation state
    private calcGuard = false;
    private lastEdited: 'amount' | 'quantity' | 'unitPrice' | null = null;
    private subs = new Subscription();

    constructor() {
        const today = new Date().toISOString().split('T')[0];
        this.transactionForm = this.fb.group({
            type: ['Expense', Validators.required],
            amount: [null, [Validators.required, Validators.min(0)]],
            categoryId: ['', Validators.required],
            date: [today, Validators.required],
            currency: ['AUD', Validators.required],
            description: ['', Validators.required],
            isTaxDeductible: [false],
            tags: [''],
            receiptUrl: [''],
            externalId: [''],
            relatedTxnId: [''],
            accountId: ['', Validators.required],
            direction: ['Outflow', Validators.required],
            counterAccountId: [''],
            instrumentId: [''],
            quantity: [null],
            unitPrice: [null],
            fees: [null],
            merchant: ['']
        });
    }

    ngOnInit(): void {
        this.loadCategories();
        this.loadAccounts();
        this.loadInstruments();
        this.transactionId = this.data?.transactionId || null;
        this.isEditMode = !!this.transactionId;
        if (this.isEditMode && this.transactionId) {
            this.loadTransaction(this.transactionId);
        } else if (this.data?.preselectedAccountId) {
            this.transactionForm.patchValue({ accountId: this.data.preselectedAccountId });
            this.transactionForm.get('accountId')!.disable();
        }
        this.setupAccountWatcher();
        this.setupAutoCalculations();
    }

    ngOnDestroy(): void { this.subs.unsubscribe(); }

    // ── Account-driven investment section ─────────────────────────────────────

    private setupAccountWatcher(): void {
        this.subs.add(
            this.transactionForm.get('accountId')!.valueChanges.subscribe(id => {
                const acc = this.accounts.find(a => a.id === id);
                this.selectedAccountAssetClass = acc?.assetClass ?? null;
                this.updateFilteredInstruments();
                // Clear investment fields when account changes
                this.transactionForm.patchValue({ instrumentId: '', quantity: null, unitPrice: null, fees: null }, { emitEvent: false });
                this.lastEdited = null;
            })
        );
        // Also re-evaluate investment section when transaction type changes
        this.subs.add(
            this.transactionForm.get('type')!.valueChanges.subscribe(() => {
                this.updateFilteredInstruments();
            })
        );
    }

    private updateFilteredInstruments(): void {
        const ac = this.selectedAccountAssetClass;
        const txnType = this.transactionForm.get('type')?.value;
        const isAssetTxn = txnType === 'AssetPurchase' || txnType === 'AssetSale';

        // For asset purchase/sale, show all instruments regardless of account type
        if (isAssetTxn) {
            this.filteredInstruments = this.instruments;
            return;
        }
        if (!ac || !INSTRUMENT_ASSET_CLASSES.has(ac)) {
            this.filteredInstruments = [];
            return;
        }
        // METAL filters to METAL instruments; others show STOCK/ETF/BOND/CRYPTO/FUND
        if (ac === 'METAL') {
            this.filteredInstruments = this.instruments.filter(i => i.instrumentType === 'METAL');
        } else {
            this.filteredInstruments = this.instruments.filter(i => i.instrumentType !== 'METAL');
        }
    }

    get showInvestmentSection(): boolean {
        // Show for tradeable account types OR when transaction type is an asset purchase/sale
        const txnType = this.transactionForm.get('type')?.value;
        const isAssetTxn = txnType === 'AssetPurchase' || txnType === 'AssetSale';
        return isAssetTxn || INSTRUMENT_ASSET_CLASSES.has(this.selectedAccountAssetClass ?? '');
    }

    // ── Auto-calculation (amount = quantity × unitPrice) ──────────────────────

    private setupAutoCalculations(): void {
        const fields: ('amount' | 'quantity' | 'unitPrice')[] = ['amount', 'quantity', 'unitPrice'];
        fields.forEach(field => {
            this.subs.add(
                this.transactionForm.get(field)!.valueChanges.subscribe(() => {
                    if (this.calcGuard) return;
                    this.lastEdited = field;
                    this.recalculate();
                })
            );
        });
    }

    private recalculate(): void {
        const amount = this.numVal('amount');
        const qty    = this.numVal('quantity');
        const price  = this.numVal('unitPrice');

        this.calcGuard = true;
        if (this.lastEdited !== 'amount' && qty && price) {
            this.transactionForm.get('amount')!.setValue(+(qty * price).toFixed(2), { emitEvent: false });
        } else if (this.lastEdited !== 'unitPrice' && qty && amount) {
            this.transactionForm.get('unitPrice')!.setValue(+(amount / qty).toFixed(6), { emitEvent: false });
        } else if (this.lastEdited !== 'quantity' && price && amount) {
            this.transactionForm.get('quantity')!.setValue(+(amount / price).toFixed(6), { emitEvent: false });
        }
        this.calcGuard = false;
    }

    private numVal(field: string): number | null {
        const v = this.transactionForm.get(field)?.value;
        return v !== null && v !== '' && !isNaN(+v) ? +v : null;
    }

    isDerived(field: string): boolean {
        return this.lastEdited !== null && this.lastEdited !== field &&
            this.numVal('quantity') !== null &&
            (this.numVal('amount') !== null || this.numVal('unitPrice') !== null);
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    loadCategories(): void {
        this.financeService.getCategories().subscribe({
            next: (categories) => { this.categories = categories; },
            error: () => this.snackBar.open('Failed to load categories', 'Close', { duration: 3000 })
        });
    }

    loadAccounts(): void {
        this.financeService.getAccounts().subscribe({
            next: (accounts) => {
                this.accounts = accounts;
                // Resolve asset class for preselected account
                if (this.data?.preselectedAccountId) {
                    const acc = accounts.find(a => a.id === this.data.preselectedAccountId);
                    this.selectedAccountAssetClass = acc?.assetClass ?? null;
                    this.updateFilteredInstruments();
                }
            },
            error: () => this.snackBar.open('Failed to load accounts', 'Close', { duration: 3000 })
        });
    }

    loadInstruments(): void {
        this.financeService.getInstruments().subscribe({
            next: (instruments) => {
                this.instruments = instruments;
                this.updateFilteredInstruments();
            },
            error: () => this.snackBar.open('Failed to load instruments', 'Close', { duration: 3000 })
        });
    }

    loadTransaction(id: string): void {
        this.loading = true;
        this.financeService.getTransaction(id).subscribe({
            next: (transaction) => {
                this.transactionForm.patchValue({
                    ...transaction,
                    type: transaction.txnType,
                    accountId: transaction.accountId,
                    direction: transaction.direction,
                    counterAccountId: transaction.counterAccountId,
                    date: transaction.txnTime ? new Date(transaction.txnTime).toISOString().split('T')[0] : ''
                });
                this.loading = false;
            },
            error: (err) => {
                console.error(err);
                this.snackBar.open('Failed to load transaction', 'Close', { duration: 5000 });
                this.loading = false;
            }
        });
    }

    onSubmit(): void {
        if (this.transactionForm.invalid) {
            this.transactionForm.markAllAsTouched();
            return;
        }
        this.loading = true;
        const formValue = this.transactionForm.getRawValue();
        const transactionRequest: any = {
            txnTime: new Date(formValue.date),
            description: formValue.description,
            txnType: formValue.type,
            amount: formValue.amount,
            currency: formValue.currency,
            categoryId: formValue.categoryId,
            isTaxDeductible: formValue.isTaxDeductible,
            tags: formValue.tags || undefined,
            receiptUrl: formValue.receiptUrl || undefined,
            externalId: formValue.externalId || undefined,
            relatedTxnId: formValue.relatedTxnId || undefined,
            accountId: formValue.accountId,
            direction: formValue.direction,
            counterAccountId: formValue.counterAccountId || undefined,
            instrumentId: formValue.instrumentId || undefined,
            quantity: formValue.quantity || undefined,
            unitPrice: formValue.unitPrice || undefined,
            fees: formValue.fees || undefined,
            merchant: formValue.merchant || undefined
        };

        if (this.isEditMode && this.transactionId) {
            const request: UpdateTransactionRequest = transactionRequest;
            this.financeService.updateTransaction(this.transactionId, request).subscribe({
                next: () => {
                    this.snackBar.open('Transaction updated successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    this.snackBar.open(err.error?.message || 'Failed to update transaction', 'Close', { duration: 5000 });
                }
            });
        } else {
            const request: CreateTransactionRequest = transactionRequest;
            this.financeService.createTransaction(request).subscribe({
                next: () => {
                    this.snackBar.open('Transaction created successfully', 'Close', { duration: 3000 });
                    this.dialogRef.close(true);
                },
                error: (err) => {
                    this.loading = false;
                    this.snackBar.open(err.error?.message || 'Failed to create transaction', 'Close', { duration: 5000 });
                }
            });
        }
    }

    cancel(): void {
        this.dialogRef.close(false);
    }
}
