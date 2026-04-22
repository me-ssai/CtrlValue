import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { InstrumentService, CreateInstrumentRequest, UpdateInstrumentRequest } from '../../../../services/instrument.service';

export interface EtfDialogData {
    instrumentId?: string;
}

@Component({
    selector: 'app-etf-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, MatDialogModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule
    ],
    template: `
        <h2 mat-dialog-title>{{ isEditMode ? 'Edit ETF / Fund' : 'Add ETF / Fund' }}</h2>
        <mat-dialog-content>
            <form [formGroup]="form" class="d-flex flex-column gap-3 mt-2">
                <div class="row g-3">
                    <div class="col-4">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Symbol</mat-label>
                            <input matInput formControlName="symbol" placeholder="VAS, IVV, ...">
                            <mat-error *ngIf="form.get('symbol')?.hasError('required')">Required</mat-error>
                        </mat-form-field>
                    </div>
                    <div class="col-8">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Name</mat-label>
                            <input matInput formControlName="name" placeholder="Vanguard Australian Shares Index ETF">
                            <mat-error *ngIf="form.get('name')?.hasError('required')">Required</mat-error>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Type</mat-label>
                            <mat-select formControlName="instrumentType">
                                <mat-option value="ETF">ETF</mat-option>
                                <mat-option value="FUND">Managed Fund</mat-option>
                            </mat-select>
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

                <mat-form-field appearance="outline">
                    <mat-label>Exchange</mat-label>
                    <input matInput formControlName="exchange" placeholder="ASX, NYSE, ...">
                </mat-form-field>

                <mat-form-field appearance="outline">
                    <mat-label>Underlying Index</mat-label>
                    <input matInput formControlName="underlyingIndex" placeholder="S&P/ASX 200, S&P 500, ...">
                </mat-form-field>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Expense Ratio (%)</mat-label>
                            <input matInput type="number" formControlName="expenseRatio" placeholder="0.07" step="0.01" min="0">
                            <span matSuffix>%</span>
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Distribution Yield (%)</mat-label>
                            <input matInput type="number" formControlName="distributionYield" placeholder="3.5" step="0.01" min="0">
                            <span matSuffix>%</span>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Distribution Frequency</mat-label>
                            <mat-select formControlName="distributionFrequency">
                                <mat-option value="Monthly">Monthly</mat-option>
                                <mat-option value="Quarterly">Quarterly</mat-option>
                                <mat-option value="Semi-annual">Semi-annual</mat-option>
                                <mat-option value="Annual">Annual</mat-option>
                            </mat-select>
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
                {{ loading ? 'Saving...' : (isEditMode ? 'Save Changes' : 'Add ETF / Fund') }}
            </button>
        </mat-dialog-actions>
    `
})
export class EtfFormComponent implements OnInit {
    form: FormGroup;
    isEditMode = false;
    instrumentId: string | null = null;
    loading = false;
    currencies = ['AUD', 'USD', 'EUR', 'GBP', 'CAD', 'JPY', 'CHF'];

    constructor(
        private fb: FormBuilder,
        private instrumentService: InstrumentService,
        private dialogRef: MatDialogRef<EtfFormComponent>,
        private snackBar: MatSnackBar,
        @Inject(MAT_DIALOG_DATA) public data: EtfDialogData
    ) {
        this.form = this.fb.group({
            symbol:                ['', [Validators.required, Validators.minLength(2), Validators.maxLength(20)]],
            name:                  ['', [Validators.required, Validators.maxLength(100)]],
            instrumentType:        ['ETF', Validators.required],
            currency:              ['AUD', Validators.required],
            exchange:              [''],
            priceProvider:         ['YAHOO_FINANCE'],
            underlyingIndex:       [''],
            expenseRatio:          [null],
            distributionYield:     [null],
            distributionFrequency: ['Quarterly']
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
                    symbol:                instrument.symbol,
                    name:                  instrument.name,
                    instrumentType:        instrument.instrumentType,
                    currency:              instrument.currency,
                    exchange:              instrument.exchange || '',
                    priceProvider:         instrument.priceProvider || 'YAHOO_FINANCE',
                    underlyingIndex:       instrument.underlyingIndex || '',
                    expenseRatio:          instrument.expenseRatio ?? null,
                    distributionYield:     instrument.distributionYield ?? null,
                    distributionFrequency: instrument.distributionFrequency || 'Quarterly'
                });
                this.loading = false;
            },
            error: () => {
                this.showSnackBar('Error loading ETF details');
                this.dialogRef.close(false);
            }
        });
    }

    onSubmit(): void {
        if (this.form.invalid) return;
        this.loading = true;
        const raw = this.form.getRawValue();

        if (this.isEditMode && this.instrumentId) {
            const request: UpdateInstrumentRequest = {
                name:                  raw.name,
                currency:              raw.currency,
                exchange:              raw.exchange || undefined,
                priceProvider:         raw.priceProvider || undefined,
                priceUnit:             'UNIT',
                underlyingIndex:       raw.underlyingIndex || undefined,
                expenseRatio:          raw.expenseRatio ?? undefined,
                distributionYield:     raw.distributionYield ?? undefined,
                distributionFrequency: raw.distributionFrequency || undefined
            };
            this.instrumentService.updateInstrument(this.instrumentId, request).subscribe({
                next: () => { this.showSnackBar('ETF updated'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error updating ETF'); this.loading = false; }
            });
        } else {
            const request: CreateInstrumentRequest = {
                symbol:                raw.symbol,
                name:                  raw.name,
                instrumentType:        raw.instrumentType,
                currency:              raw.currency,
                exchange:              raw.exchange || undefined,
                priceProvider:         raw.priceProvider || undefined,
                priceUnit:             'UNIT',
                underlyingIndex:       raw.underlyingIndex || undefined,
                expenseRatio:          raw.expenseRatio ?? undefined,
                distributionYield:     raw.distributionYield ?? undefined,
                distributionFrequency: raw.distributionFrequency || undefined
            };
            this.instrumentService.createInstrument(request).subscribe({
                next: () => { this.showSnackBar('ETF added'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error adding ETF'); this.loading = false; }
            });
        }
    }

    cancel(): void { this.dialogRef.close(false); }

    private showSnackBar(message: string): void {
        this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
    }
}
