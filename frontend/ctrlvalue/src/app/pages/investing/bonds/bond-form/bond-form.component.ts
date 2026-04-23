import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InstrumentService, CreateInstrumentRequest, UpdateInstrumentRequest } from '../../../../services/instrument.service';

export interface BondDialogData {
    instrumentId?: string;
}

@Component({
    selector: 'app-bond-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, MatDialogModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
        MatDatepickerModule, MatNativeDateModule
    ],
    template: `
        <h2 mat-dialog-title>{{ isEditMode ? 'Edit Bond' : 'Add Bond' }}</h2>
        <mat-dialog-content>
            <form [formGroup]="form" class="d-flex flex-column gap-3 mt-2">
                <div class="row g-3">
                    <div class="col-4">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Symbol / ISIN</mat-label>
                            <input matInput formControlName="symbol" placeholder="AU000000XYZ1">
                            <mat-error *ngIf="form.get('symbol')?.hasError('required')">Required</mat-error>
                        </mat-form-field>
                    </div>
                    <div class="col-8">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Bond Name</mat-label>
                            <input matInput formControlName="name" placeholder="Australian Gov. Bond 2030">
                            <mat-error *ngIf="form.get('name')?.hasError('required')">Required</mat-error>
                        </mat-form-field>
                    </div>
                </div>

                <mat-form-field appearance="outline">
                    <mat-label>Issuer</mat-label>
                    <input matInput formControlName="issuer" placeholder="Australian Government, Commonwealth Bank, ...">
                </mat-form-field>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Face Value</mat-label>
                            <input matInput type="number" formControlName="faceValue" placeholder="1000" min="0">
                            <span matPrefix>$&nbsp;</span>
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Currency</mat-label>
                            <mat-select formControlName="currency">
                                <mat-option *ngFor="let c of currencies" [value]="c">{{ c }}</mat-option>
                            </mat-select>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Coupon Rate (%)</mat-label>
                            <input matInput type="number" formControlName="couponRate" placeholder="4.25" step="0.01" min="0">
                            <span matSuffix>%</span>
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Coupon Frequency</mat-label>
                            <mat-select formControlName="couponFrequency">
                                <mat-option value="Monthly">Monthly</mat-option>
                                <mat-option value="Quarterly">Quarterly</mat-option>
                                <mat-option value="Semi-annual">Semi-annual</mat-option>
                                <mat-option value="Annual">Annual</mat-option>
                            </mat-select>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Issue Date</mat-label>
                            <input matInput [matDatepicker]="issuePicker" formControlName="issueDate">
                            <mat-datepicker-toggle matIconSuffix [for]="issuePicker"></mat-datepicker-toggle>
                            <mat-datepicker #issuePicker></mat-datepicker>
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Maturity Date</mat-label>
                            <input matInput [matDatepicker]="maturityPicker" formControlName="maturityDate">
                            <mat-datepicker-toggle matIconSuffix [for]="maturityPicker"></mat-datepicker-toggle>
                            <mat-datepicker #maturityPicker></mat-datepicker>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Credit Rating</mat-label>
                            <input matInput formControlName="creditRating" placeholder="AAA, AA+, BBB, ...">
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Price Provider</mat-label>
                            <mat-select formControlName="priceProvider">
                                <mat-option value="YAHOO_FINANCE">Yahoo Finance</mat-option>
                                <mat-option value="MANUAL">Manual</mat-option>
                            </mat-select>
                        </mat-form-field>
                    </div>
                </div>
            </form>
        </mat-dialog-content>
        <mat-dialog-actions align="end">
            <button mat-button (click)="cancel()">Cancel</button>
            <button mat-raised-button color="primary" (click)="onSubmit()" [disabled]="form.invalid || loading">
                {{ loading ? 'Saving...' : (isEditMode ? 'Save Changes' : 'Add Bond') }}
            </button>
        </mat-dialog-actions>
    `
})
export class BondFormComponent implements OnInit {
    private fb = inject(FormBuilder);
    private instrumentService = inject(InstrumentService);
    private dialogRef = inject<MatDialogRef<BondFormComponent>>(MatDialogRef);
    private snackBar = inject(MatSnackBar);
    data = inject<BondDialogData>(MAT_DIALOG_DATA);

    form: FormGroup;
    isEditMode = false;
    instrumentId: string | null = null;
    loading = false;
    currencies = ['AUD', 'USD', 'EUR', 'GBP', 'CAD', 'JPY', 'CHF'];

    constructor() {
        this.form = this.fb.group({
            symbol:          ['', [Validators.required, Validators.minLength(2), Validators.maxLength(20)]],
            name:            ['', [Validators.required, Validators.maxLength(100)]],
            currency:        ['AUD', Validators.required],
            priceProvider:   ['MANUAL'],
            issuer:          [''],
            faceValue:       [null],
            couponRate:      [null],
            couponFrequency: ['Semi-annual'],
            issueDate:       [null],
            maturityDate:    [null],
            creditRating:    ['']
        });
    }

    ngOnInit(): void {
        this.instrumentId = this.data?.instrumentId || null;
        if (this.instrumentId) {
            this.isEditMode = true;
            this.loadInstrument(this.instrumentId);
            this.form.get('symbol')?.disable();
        }
    }

    loadInstrument(id: string): void {
        this.loading = true;
        this.instrumentService.getInstrumentById(id).subscribe({
            next: (instrument) => {
                this.form.patchValue({
                    symbol:          instrument.symbol,
                    name:            instrument.name,
                    currency:        instrument.currency,
                    priceProvider:   instrument.priceProvider || 'MANUAL',
                    issuer:          instrument.issuer || '',
                    faceValue:       instrument.faceValue ?? null,
                    couponRate:      instrument.couponRate ?? null,
                    couponFrequency: instrument.couponFrequency || 'Semi-annual',
                    issueDate:       instrument.issueDate ? new Date(instrument.issueDate) : null,
                    maturityDate:    instrument.maturityDate ? new Date(instrument.maturityDate) : null,
                    creditRating:    instrument.creditRating || ''
                });
                this.loading = false;
            },
            error: () => {
                this.showSnackBar('Error loading bond details');
                this.dialogRef.close(false);
            }
        });
    }

    onSubmit(): void {
        if (this.form.invalid) return;
        this.loading = true;
        const raw = this.form.getRawValue();

        const toIso = (d: Date | null | undefined) =>
            d instanceof Date ? d.toISOString() : (d ?? undefined);

        if (this.isEditMode && this.instrumentId) {
            const request: UpdateInstrumentRequest = {
                name:                  raw.name,
                currency:              raw.currency,
                priceProvider:         raw.priceProvider || undefined,
                priceUnit:             'UNIT',
                issuer:                raw.issuer || undefined,
                faceValue:             raw.faceValue ?? undefined,
                couponRate:            raw.couponRate ?? undefined,
                couponFrequency:       raw.couponFrequency || undefined,
                issueDate:             toIso(raw.issueDate),
                maturityDate:          toIso(raw.maturityDate),
                creditRating:          raw.creditRating || undefined
            };
            this.instrumentService.updateInstrument(this.instrumentId, request).subscribe({
                next: () => { this.showSnackBar('Bond updated'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error updating bond'); this.loading = false; }
            });
        } else {
            const request: CreateInstrumentRequest = {
                symbol:                raw.symbol,
                name:                  raw.name,
                instrumentType:        'BOND',
                currency:              raw.currency,
                priceProvider:         raw.priceProvider || undefined,
                priceUnit:             'UNIT',
                issuer:                raw.issuer || undefined,
                faceValue:             raw.faceValue ?? undefined,
                couponRate:            raw.couponRate ?? undefined,
                couponFrequency:       raw.couponFrequency || undefined,
                issueDate:             toIso(raw.issueDate),
                maturityDate:          toIso(raw.maturityDate),
                creditRating:          raw.creditRating || undefined
            };
            this.instrumentService.createInstrument(request).subscribe({
                next: () => { this.showSnackBar('Bond added'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error adding bond'); this.loading = false; }
            });
        }
    }

    cancel(): void { this.dialogRef.close(false); }

    private showSnackBar(message: string): void {
        this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
    }
}
