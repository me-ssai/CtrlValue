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
import { InstrumentService, PositionService, Instrument } from '../../../../services/instrument.service';
import { FinanceService } from '../../../../services/finance.service';
import { Account } from '../../../../models/api.models';

export interface MetalHoldingDialogData {
    positionId?: string;
}

/** Known metal symbols mapped to friendly names */
const METAL_DISPLAY: Record<string, string> = {
    XAU: 'Gold',
    XAG: 'Silver',
    XPT: 'Platinum',
    XPD: 'Palladium'
};

@Component({
    selector: 'app-metal-holding-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, MatDialogModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
        MatDatepickerModule, MatNativeDateModule
    ],
    template: `
        <h2 mat-dialog-title>{{ isEditMode ? 'Edit Metal Holding' : 'Add Metal Holding' }}</h2>
        <mat-dialog-content>
            <form [formGroup]="form" class="d-flex flex-column gap-3 mt-2">
                <mat-form-field appearance="outline">
                    <mat-label>Metal</mat-label>
                    <mat-select formControlName="instrumentId" [disabled]="isEditMode">
                        <mat-option *ngFor="let metal of metals" [value]="metal.id">
                            {{ metalDisplayName(metal) }}
                        </mat-option>
                    </mat-select>
                    <mat-hint *ngIf="metals.length === 0">No metal instruments found. Add Gold/Silver instruments first.</mat-hint>
                </mat-form-field>

                <mat-form-field appearance="outline">
                    <mat-label>Account</mat-label>
                    <mat-select formControlName="accountId" [disabled]="isEditMode">
                        <mat-option *ngFor="let account of accounts" [value]="account.id">
                            {{ account.name }}
                        </mat-option>
                    </mat-select>
                </mat-form-field>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Quantity</mat-label>
                            <input matInput type="number" formControlName="quantity" placeholder="5" min="0" step="0.001">
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Unit</mat-label>
                            <mat-select formControlName="unit">
                                <mat-option value="TROY_OZ">Troy oz</mat-option>
                                <mat-option value="GRAM">Gram</mat-option>
                                <mat-option value="KILOGRAM">Kilogram</mat-option>
                                <mat-option value="TOLA">Tola</mat-option>
                                <mat-option value="UNIT">Unit</mat-option>
                            </mat-select>
                        </mat-form-field>
                    </div>
                </div>

                <div class="row g-3">
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Total Purchase Price</mat-label>
                            <input matInput type="number" formControlName="costBasisTotal" placeholder="9750" min="0" step="0.01"
                                (input)="recalcPerUnit()">
                            <span matPrefix>$&nbsp;</span>
                        </mat-form-field>
                    </div>
                    <div class="col-6">
                        <mat-form-field appearance="outline" class="w-100">
                            <mat-label>Price per Unit</mat-label>
                            <input matInput type="number" formControlName="pricePerUnit" placeholder="1950" min="0" step="0.01"
                                (input)="recalcTotal()">
                            <span matPrefix>$&nbsp;</span>
                        </mat-form-field>
                    </div>
                </div>

                <mat-form-field appearance="outline">
                    <mat-label>Purchase Date</mat-label>
                    <input matInput [matDatepicker]="picker" formControlName="openedAt">
                    <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
                    <mat-datepicker #picker></mat-datepicker>
                </mat-form-field>
            </form>
        </mat-dialog-content>
        <mat-dialog-actions align="end">
            <button mat-button (click)="cancel()">Cancel</button>
            <button mat-raised-button color="primary" (click)="onSubmit()" [disabled]="form.invalid || loading">
                {{ loading ? 'Saving...' : (isEditMode ? 'Save Changes' : 'Add Holding') }}
            </button>
        </mat-dialog-actions>
    `
})
export class MetalHoldingFormComponent implements OnInit {
    private fb = inject(FormBuilder);
    private instrumentService = inject(InstrumentService);
    private positionService = inject(PositionService);
    private financeService = inject(FinanceService);
    private dialogRef = inject<MatDialogRef<MetalHoldingFormComponent>>(MatDialogRef);
    private snackBar = inject(MatSnackBar);
    data = inject<MetalHoldingDialogData>(MAT_DIALOG_DATA);

    form: FormGroup;
    isEditMode = false;
    positionId: string | null = null;
    loading = false;
    metals: Instrument[] = [];
    accounts: Account[] = [];
    private updating = false;

    constructor() {
        this.form = this.fb.group({
            instrumentId: ['', Validators.required],
            accountId: ['', Validators.required],
            quantity: [null, [Validators.required, Validators.min(0.000001)]],
            unit: ['TROY_OZ', Validators.required],
            costBasisTotal: [null, [Validators.required, Validators.min(0)]],
            pricePerUnit: [null],
            openedAt: [new Date(), Validators.required]
        });
    }

    ngOnInit(): void {
        this.loadMetals();
        this.loadAccounts();
        this.positionId = this.data?.positionId || null;
        if (this.positionId) {
            this.isEditMode = true;
            this.loadPosition(this.positionId);
        }
    }

    loadMetals(): void {
        this.instrumentService.getInstruments('METAL').subscribe({
            next: (instruments) => { this.metals = instruments; },
            error: () => console.error('Could not load metal instruments')
        });
    }

    loadAccounts(): void {
        this.financeService.getAccounts().subscribe({
            next: (accounts) => { this.accounts = accounts; },
            error: () => console.error('Could not load accounts')
        });
    }

    loadPosition(id: string): void {
        this.loading = true;
        this.positionService.getPositionById(id).subscribe({
            next: (pos) => {
                const pricePerUnit = pos.quantity && pos.costBasisTotal
                    ? pos.costBasisTotal / pos.quantity : null;
                this.form.patchValue({
                    instrumentId: pos.instrumentId,
                    accountId: pos.accountId,
                    quantity: pos.quantity,
                    unit: pos.unit,
                    costBasisTotal: pos.costBasisTotal,
                    pricePerUnit,
                    openedAt: pos.openedAt ? new Date(pos.openedAt) : new Date()
                });
                this.loading = false;
            },
            error: () => {
                this.showSnackBar('Error loading holding');
                this.dialogRef.close(false);
            }
        });
    }

    recalcPerUnit(): void {
        if (this.updating) return;
        const qty = this.form.value.quantity;
        const total = this.form.value.costBasisTotal;
        if (qty && total && qty > 0) {
            this.updating = true;
            this.form.patchValue({ pricePerUnit: total / qty }, { emitEvent: false });
            this.updating = false;
        }
    }

    recalcTotal(): void {
        if (this.updating) return;
        const qty = this.form.value.quantity;
        const ppu = this.form.value.pricePerUnit;
        if (qty && ppu && ppu > 0) {
            this.updating = true;
            this.form.patchValue({ costBasisTotal: qty * ppu }, { emitEvent: false });
            this.updating = false;
        }
    }

    metalDisplayName(metal: Instrument): string {
        return METAL_DISPLAY[metal.symbol] ? `${METAL_DISPLAY[metal.symbol]} (${metal.symbol})` : `${metal.name} (${metal.symbol})`;
    }

    onSubmit(): void {
        if (this.form.invalid) return;
        this.loading = true;
        const raw = this.form.getRawValue();
        const openedAt = raw.openedAt instanceof Date
            ? raw.openedAt.toISOString()
            : raw.openedAt;

        if (this.isEditMode && this.positionId) {
            this.positionService.updatePosition(this.positionId, {
                quantity: raw.quantity,
                costBasisTotal: raw.costBasisTotal
            }).subscribe({
                next: () => { this.showSnackBar('Holding updated'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error updating holding'); this.loading = false; }
            });
        } else {
            this.positionService.createPosition({
                accountId: raw.accountId,
                instrumentId: raw.instrumentId,
                quantity: raw.quantity,
                unit: raw.unit,
                costBasisTotal: raw.costBasisTotal,
                openedAt
            }).subscribe({
                next: () => { this.showSnackBar('Holding added'); this.dialogRef.close(true); },
                error: () => { this.showSnackBar('Error adding holding'); this.loading = false; }
            });
        }
    }

    cancel(): void { this.dialogRef.close(false); }

    private showSnackBar(message: string): void {
        this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
    }
}
