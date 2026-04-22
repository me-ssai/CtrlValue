import { Component, Input, OnChanges, SimpleChanges, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';

import { LoanService, LoanDetailsDto, LoanRateHistoryDto, LoanRateChangeRequest } from '../../../services/loan.service';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../services/api.generated';

@Component({
    selector: 'app-loan-detail-form',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatCardModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatButtonModule,
        MatCheckboxModule,
        MatDatepickerModule,
        MatNativeDateModule,
        MatDividerModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
        MatTooltipModule,
        MatExpansionModule,
        MatChipsModule,
    ],
    templateUrl: './loan-detail-form.component.html',
    styleUrl: './loan-detail-form.component.scss'
})
export class LoanDetailFormComponent implements OnInit, OnChanges {
    @Input() accountId!: string;
    @Input() entityId!: string;

    loan: LoanDetailsDto | null = null;
    loanForm!: FormGroup;
    rateChangeForm!: FormGroup;

    loading = true;
    saving = false;
    addingRate = false;
    isNew = true;
    showRateChangeForm = false;

    propertyAccounts: AccountDto[] = [];
    offsetAccounts: AccountDto[] = [];

    readonly frequencies = ['Monthly', 'Fortnightly', 'Weekly'];
    readonly rateTypes = ['Variable', 'Fixed'];

    constructor(
        private fb: FormBuilder,
        private loanService: LoanService,
        private financeService: FinanceService,
        private snackBar: MatSnackBar
    ) { }

    ngOnInit(): void {
        this.loadSupportingAccounts();
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['accountId'] && this.accountId) {
            this.loadLoan();
        }
    }

    private loadSupportingAccounts(): void {
        this.financeService.getAccounts().subscribe(accounts => {
            this.propertyAccounts = accounts.filter(a =>
                a.accountType === 'ASSET' && a.assetClass === 'PROPERTY' && a.isActive
            );
            this.offsetAccounts = accounts.filter(a =>
                a.isOffsetAccount && a.isActive
            );
        });
    }

    private loadLoan(): void {
        this.loading = true;
        this.loanService.getLoanByAccount(this.accountId).subscribe({
            next: (loan) => {
                this.loan = loan;
                this.isNew = false;
                this.buildForm(loan);
                this.loading = false;
            },
            error: (err) => {
                // 404 means no loan yet — show blank create form
                this.loan = null;
                this.isNew = true;
                this.buildForm(null);
                this.loading = false;
            }
        });
    }

    private buildForm(loan: LoanDetailsDto | null): void {
        const today = new Date();
        this.loanForm = this.fb.group({
            propertyAccountId: [loan?.propertyAccountId ?? null],
            offsetAccountId: [loan?.offsetAccountId ?? null],
            loanAmount: [loan?.loanAmount ?? null, [Validators.required, Validators.min(0.01)]],
            interestRate: [loan ? loan.interestRate! * 100 : null, [Validators.required, Validators.min(0), Validators.max(100)]],
            rateType: [loan?.rateType ?? 'Variable', Validators.required],
            fixedRateExpiresAt: [loan?.fixedRateExpiresAt ? new Date(loan.fixedRateExpiresAt) : null],
            paymentFrequency: [loan?.paymentFrequency ?? 'Monthly', Validators.required],
            repaymentAmount: [loan?.repaymentAmount ?? null, [Validators.required, Validators.min(0.01)]],
            loanTermYears: [loan ? Math.floor(loan.loanTermMonths! / 12) : 30, [Validators.required, Validators.min(1)]],
            loanTermMonths: [loan ? loan.loanTermMonths! % 12 : 0, [Validators.min(0), Validators.max(11)]],
            startDate: [loan?.startDate ? new Date(loan.startDate) : today, Validators.required],
            nextPaymentDate: [loan?.nextPaymentDate ? new Date(loan.nextPaymentDate) : null],
            isInterestOnly: [loan?.isInterestOnly ?? false],
            notes: [loan?.notes ?? '']
        });

        this.rateChangeForm = this.fb.group({
            rate: [null, [Validators.required, Validators.min(0), Validators.max(100)]],
            effectiveFrom: [new Date(), Validators.required],
            notes: ['']
        });
    }

    get isFixedRate(): boolean {
        return this.loanForm?.get('rateType')?.value === 'Fixed';
    }

    save(): void {
        if (this.loanForm.invalid) { this.loanForm.markAllAsTouched(); return; }
        this.saving = true;
        const v = this.loanForm.value;
        const termMonths = v.loanTermYears * 12 + (v.loanTermMonths || 0);
        const rateDecimal = v.interestRate / 100;

        if (this.isNew) {
            const req = {
                accountId: this.accountId,
                propertyAccountId: v.propertyAccountId || undefined,
                offsetAccountId: v.offsetAccountId || undefined,
                loanAmount: v.loanAmount,
                interestRate: rateDecimal,
                rateType: v.rateType,
                fixedRateExpiresAt: v.fixedRateExpiresAt || undefined,
                paymentFrequency: v.paymentFrequency,
                repaymentAmount: v.repaymentAmount,
                loanTermMonths: termMonths,
                startDate: v.startDate,
                isInterestOnly: v.isInterestOnly,
                notes: v.notes || undefined
            };
            this.loanService.createLoan(req).subscribe({
                next: (loan) => { this.loan = loan; this.isNew = false; this.saving = false; this.snackBar.open('Loan details saved.', 'OK', { duration: 3000 }); },
                error: () => { this.saving = false; this.snackBar.open('Failed to save loan details.', 'OK', { duration: 4000 }); }
            });
        } else {
            const req = {
                propertyAccountId: v.propertyAccountId || undefined,
                offsetAccountId: v.offsetAccountId || undefined,
                loanAmount: v.loanAmount,
                interestRate: rateDecimal,
                rateType: v.rateType,
                fixedRateExpiresAt: v.fixedRateExpiresAt || undefined,
                paymentFrequency: v.paymentFrequency,
                repaymentAmount: v.repaymentAmount,
                loanTermMonths: termMonths,
                startDate: v.startDate,
                nextPaymentDate: v.nextPaymentDate || this.loan!.nextPaymentDate!,
                isInterestOnly: v.isInterestOnly,
                notes: v.notes || undefined
            };
            this.loanService.updateLoan(this.loan!.id!, req).subscribe({
                next: (loan) => { this.loan = loan; this.saving = false; this.snackBar.open('Loan details updated.', 'OK', { duration: 3000 }); },
                error: () => { this.saving = false; this.snackBar.open('Failed to update loan details.', 'OK', { duration: 4000 }); }
            });
        }
    }

    submitRateChange(): void {
        if (this.rateChangeForm.invalid) { this.rateChangeForm.markAllAsTouched(); return; }
        this.addingRate = true;
        const v = this.rateChangeForm.value;
        const req: LoanRateChangeRequest = {
            rate: v.rate / 100,
            effectiveFrom: v.effectiveFrom,
            notes: v.notes || undefined
        };
        this.loanService.addRateChange(this.loan!.id!, req).subscribe({
            next: (loan) => {
                this.loan = loan;
                this.addingRate = false;
                this.showRateChangeForm = false;
                this.rateChangeForm.reset({ effectiveFrom: new Date() });
                this.snackBar.open('Rate change recorded.', 'OK', { duration: 3000 });
            },
            error: () => { this.addingRate = false; this.snackBar.open('Failed to record rate change.', 'OK', { duration: 4000 }); }
        });
    }

    formatRate(rate: number): string {
        return (rate * 100).toFixed(2) + '%';
    }
}
