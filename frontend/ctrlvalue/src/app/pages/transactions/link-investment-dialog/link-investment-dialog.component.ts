import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FinanceService } from '../../../services/finance.service';
import { TransactionDto, InstrumentDto, UpdateTransactionRequest } from '../../../models/api.models';

export interface LinkInvestmentDialogData {
    transaction: TransactionDto;
}

interface InvestmentType {
    key: string;
    label: string;
    icon: string;
    description: string;
}

@Component({
    selector: 'app-link-investment-dialog',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatCardModule,
        MatSnackBarModule,
        MatProgressSpinnerModule
    ],
    templateUrl: './link-investment-dialog.component.html',
    styleUrls: ['./link-investment-dialog.component.scss']
})
export class LinkInvestmentDialogComponent implements OnInit {
    transaction: TransactionDto;
    selectedType: string | null = null;
    instruments: InstrumentDto[] = [];
    filteredInstruments: InstrumentDto[] = [];
    loading = false;
    saving = false;

    linkForm: FormGroup;

    investmentTypes: InvestmentType[] = [
        { key: 'STOCK', label: 'Stocks / ETFs', icon: 'candlestick_chart', description: 'Link to a stock, ETF or fund purchase' },
        { key: 'METAL', label: 'Precious Metals', icon: 'diamond', description: 'Link to a gold, silver or other metal purchase' },
        { key: 'CRYPTO', label: 'Cryptocurrency', icon: 'currency_bitcoin', description: 'Link to a crypto purchase' },
    ];

    constructor(
        private fb: FormBuilder,
        private financeService: FinanceService,
        private dialogRef: MatDialogRef<LinkInvestmentDialogComponent>,
        private snackBar: MatSnackBar,
        @Inject(MAT_DIALOG_DATA) public data: LinkInvestmentDialogData
    ) {
        this.transaction = data.transaction;
        this.linkForm = this.fb.group({
            instrumentId: ['', Validators.required],
            quantity: [null, [Validators.required, Validators.min(0.000001)]],
            unitPrice: [null, [Validators.required, Validators.min(0)]],
            fees: [null]
        });
    }

    ngOnInit(): void {
        this.loading = true;
        this.financeService.getInstruments().subscribe({
            next: (instruments) => {
                this.instruments = instruments;
                this.loading = false;
                // Pre-fill if already linked
                if (this.transaction.instrumentId) {
                    const inst = instruments.find(i => i.id === this.transaction.instrumentId);
                    if (inst) {
                        this.selectedType = this.getTypeForInstrument(inst);
                        this.filterInstruments();
                        this.linkForm.patchValue({
                            instrumentId: this.transaction.instrumentId,
                            quantity: this.transaction.quantity,
                            unitPrice: this.transaction.unitPrice,
                            fees: this.transaction.fees
                        });
                    }
                } else {
                    // Pre-fill unit price from transaction amount
                    this.linkForm.patchValue({ unitPrice: this.transaction.amount });
                }
            },
            error: () => {
                this.loading = false;
                this.snackBar.open('Failed to load instruments', 'Close', { duration: 3000 });
            }
        });
    }

    selectType(type: string): void {
        this.selectedType = type;
        this.filterInstruments();
        this.linkForm.patchValue({ instrumentId: '', quantity: null });
    }

    private filterInstruments(): void {
        if (this.selectedType === 'METAL') {
            this.filteredInstruments = this.instruments.filter(i => i.instrumentType === 'METAL');
        } else if (this.selectedType === 'CRYPTO') {
            this.filteredInstruments = this.instruments.filter(i => i.instrumentType === 'CRYPTO');
        } else {
            this.filteredInstruments = this.instruments.filter(i => i.instrumentType !== 'METAL' && i.instrumentType !== 'CRYPTO');
        }
    }

    get isAlreadyLinked(): boolean {
        return !!this.transaction.instrumentId;
    }

    get selectedInstrumentName(): string {
        const id = this.linkForm.get('instrumentId')?.value;
        return this.instruments.find(i => i.id === id)?.name ?? '';
    }

    save(): void {
        if (this.linkForm.invalid) {
            this.linkForm.markAllAsTouched();
            return;
        }
        this.saving = true;
        const v = this.linkForm.value;

        // Build the full update request from the existing transaction + new investment fields
        const request: UpdateTransactionRequest = {
            txnTime: this.transaction.txnTime as any,
            description: this.transaction.description ?? '',
            txnType: (this.transaction.direction === 'Outflow' ? 'AssetPurchase' : 'AssetSale') as any,
            amount: this.transaction.amount ?? 0,
            currency: this.transaction.currency ?? 'AUD',
            categoryId: this.transaction.categoryId ?? undefined,
            accountId: this.transaction.accountId ?? '',
            direction: this.transaction.direction as any,
            counterAccountId: this.transaction.counterAccountId ?? undefined,
            instrumentId: v.instrumentId,
            quantity: v.quantity,
            unitPrice: v.unitPrice,
            fees: v.fees ?? undefined,
            isTaxDeductible: this.transaction.isTaxDeductible ?? false,
            tags: this.transaction.tags ?? undefined,
            receiptUrl: this.transaction.receiptUrl ?? undefined,
            externalId: this.transaction.externalId ?? undefined,
            relatedTxnId: this.transaction.relatedTxnId ?? undefined,
            merchant: this.transaction.merchant ?? undefined
        };

        this.financeService.updateTransaction(this.transaction.id!, request).subscribe({
            next: () => {
                this.snackBar.open('Investment linked successfully', 'OK', { duration: 3000 });
                this.dialogRef.close(true);
            },
            error: (err) => {
                this.saving = false;
                this.snackBar.open(err.error?.message || 'Failed to link investment', 'Close', { duration: 5000 });
            }
        });
    }

    unlink(): void {
        if (!confirm('Remove the investment link from this transaction?')) return;
        this.saving = true;
        const request: UpdateTransactionRequest = {
            txnTime: this.transaction.txnTime as any,
            description: this.transaction.description ?? '',
            txnType: (this.transaction.direction === 'Outflow' ? 'Expense' : 'Income') as any,
            amount: this.transaction.amount ?? 0,
            currency: this.transaction.currency ?? 'AUD',
            categoryId: this.transaction.categoryId ?? undefined,
            accountId: this.transaction.accountId ?? '',
            direction: this.transaction.direction as any,
            instrumentId: undefined,
            quantity: undefined,
            unitPrice: undefined,
            fees: undefined,
            isTaxDeductible: this.transaction.isTaxDeductible ?? false
        };
        this.financeService.updateTransaction(this.transaction.id!, request).subscribe({
            next: () => {
                this.snackBar.open('Investment link removed', 'OK', { duration: 3000 });
                this.dialogRef.close(true);
            },
            error: () => {
                this.saving = false;
                this.snackBar.open('Failed to remove link', 'Close', { duration: 5000 });
            }
        });
    }

    cancel(): void { this.dialogRef.close(false); }

    getSelectedTypeIcon(): string {
        return this.investmentTypes.find(t => t.key === this.selectedType)?.icon ?? 'show_chart';
    }

    getSelectedTypeLabel(): string {
        return this.investmentTypes.find(t => t.key === this.selectedType)?.label ?? '';
    }

    private getTypeForInstrument(inst: InstrumentDto): string {
        if (inst.instrumentType === 'METAL') return 'METAL';
        if (inst.instrumentType === 'CRYPTO') return 'CRYPTO';
        return 'STOCK';
    }
}
