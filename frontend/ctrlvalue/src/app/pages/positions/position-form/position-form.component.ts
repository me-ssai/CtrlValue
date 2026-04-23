import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subscription } from 'rxjs';
import { PositionService, InstrumentService, Instrument } from '../../../services/instrument.service';
import { FinanceService } from '../../../services/finance.service';
import { Account } from '../../../models/api.models';

export interface PositionDialogData {
  positionId?: string;
}

const METAL_UNITS = ['UNIT', 'TROY_OZ', 'GRAM', 'KILOGRAM', 'TOLA'];
const METAL_UNIT_LABELS: Record<string, string> = {
  UNIT: 'Units', TROY_OZ: 'Troy oz', GRAM: 'Gram', KILOGRAM: 'Kilogram', TOLA: 'Tola'
};

@Component({
  selector: 'app-position-form',
  standalone: true,
  imports: [
    CommonModule, CurrencyPipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
    MatDatepickerModule, MatNativeDateModule, MatTooltipModule
  ],
  templateUrl: './position-form.component.html',
  styleUrl: './position-form.component.scss'
})
export class PositionFormComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private positionService = inject(PositionService);
  private instrumentService = inject(InstrumentService);
  private financeService = inject(FinanceService);
  private dialogRef = inject<MatDialogRef<PositionFormComponent>>(MatDialogRef);
  private snackBar = inject(MatSnackBar);
  data = inject<PositionDialogData>(MAT_DIALOG_DATA);

  positionForm: FormGroup;
  isEditMode = false;
  positionId: string | null = null;
  loading = false;
  accounts: Account[] = [];
  instruments: Instrument[] = [];
  selectedAccountBalance: number | null = null;
  selectedAccountCurrency = 'AUD';

  readonly metalUnits = METAL_UNITS;
  readonly metalUnitLabels = METAL_UNIT_LABELS;

  private calcGuard = false;
  private lastEdited: 'costBasisTotal' | 'costBasisPerUnit' | 'quantity' | null = null;
  private subs = new Subscription();

  constructor() {
    this.positionForm = this.fb.group({
      accountId: ['', Validators.required],
      instrumentId: ['', Validators.required],
      quantity: [null, [Validators.required, Validators.min(0)]],
      unit: ['UNIT', Validators.required],
      costBasisTotal: [null, [Validators.required, Validators.min(0)]],
      costBasisPerUnit: [null],   // derived — not sent to backend
      openedAt: [new Date(), Validators.required]
    });
  }

  ngOnInit(): void {
    this.loadAccounts();
    this.loadInstruments();
    this.positionId = this.data?.positionId || null;
    if (this.positionId) {
      this.isEditMode = true;
      this.loadPosition(this.positionId);
      this.positionForm.get('accountId')?.disable();
      this.positionForm.get('instrumentId')?.disable();
      this.positionForm.get('openedAt')?.disable();
      this.positionForm.get('unit')?.disable();
    }
    this.setupAutoCalculations();
    this.setupAccountBalanceWatch();
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  private setupAccountBalanceWatch(): void {
    this.subs.add(
      this.positionForm.get('accountId')!.valueChanges.subscribe((accountId: string) => {
        const account = this.accounts.find(a => a.id === accountId);
        this.selectedAccountBalance = account?.currentBalance ?? null;
        this.selectedAccountCurrency = account?.currency ?? 'AUD';
      })
    );
  }

  // ── Auto-calculation ──────────────────────────────────────────────────────

  private setupAutoCalculations(): void {
    const fields: ('costBasisTotal' | 'costBasisPerUnit' | 'quantity')[] =
      ['costBasisTotal', 'costBasisPerUnit', 'quantity'];

    fields.forEach(field => {
      this.subs.add(
        this.positionForm.get(field)!.valueChanges.subscribe(() => {
          if (this.calcGuard) return;
          this.lastEdited = field;
          this.recalculate();
        })
      );
    });
  }

  private recalculate(): void {
    const qty   = this.numVal('quantity');
    const total = this.numVal('costBasisTotal');
    const per   = this.numVal('costBasisPerUnit');

    this.calcGuard = true;
    if (this.lastEdited !== 'costBasisTotal' && qty && per) {
      this.positionForm.get('costBasisTotal')!.setValue(+(qty * per).toFixed(2), { emitEvent: false });
    } else if (this.lastEdited !== 'costBasisPerUnit' && qty && total) {
      this.positionForm.get('costBasisPerUnit')!.setValue(+(total / qty).toFixed(6), { emitEvent: false });
    } else if (this.lastEdited !== 'quantity' && per && total) {
      this.positionForm.get('quantity')!.setValue(+(total / per).toFixed(6), { emitEvent: false });
    }
    this.calcGuard = false;
  }

  private numVal(field: string): number | null {
    const v = this.positionForm.get(field)?.value;
    return v !== null && v !== '' && !isNaN(+v) ? +v : null;
  }

  isDerived(field: string): boolean {
    return this.lastEdited !== null && this.lastEdited !== field &&
      this.numVal('quantity') !== null &&
      (this.numVal('costBasisTotal') !== null || this.numVal('costBasisPerUnit') !== null);
  }

  // ── Data loading ─────────────────────────────────────────────────────────

  loadAccounts(): void {
    this.financeService.getAccounts().subscribe({
      next: (accounts) => this.accounts = accounts,
      error: (error) => console.error('Error loading accounts:', error)
    });
  }

  loadInstruments(): void {
    this.instrumentService.getInstruments().subscribe({
      next: (instruments) => this.instruments = instruments,
      error: (error) => console.error('Error loading instruments:', error)
    });
  }

  loadPosition(id: string): void {
    this.loading = true;
    this.positionService.getPositionById(id).subscribe({
      next: (position) => {
        this.positionForm.patchValue({
          ...position,
          openedAt: new Date(position.openedAt)
        });
        this.loading = false;
      },
      error: () => {
        this.showSnackBar('Error loading position details');
        this.dialogRef.close(false);
      }
    });
  }

  onSubmit(): void {
    if (this.positionForm.invalid) return;
    this.loading = true;
    const formValue = this.positionForm.getRawValue();
    const { costBasisPerUnit: _costBasisPerUnit, ...rest } = formValue;
    const openedAt = formValue.openedAt instanceof Date ? formValue.openedAt.toISOString() : formValue.openedAt;
    const request = { ...rest, openedAt };

    if (this.isEditMode && this.positionId) {
      this.positionService.updatePosition(this.positionId, request).subscribe({
        next: () => { this.showSnackBar('Position updated successfully'); this.dialogRef.close(true); },
        error: () => { this.showSnackBar('Error updating position'); this.loading = false; }
      });
    } else {
      this.positionService.createPosition(request).subscribe({
        next: () => { this.showSnackBar('Position created successfully'); this.dialogRef.close(true); },
        error: () => { this.showSnackBar('Error creating position'); this.loading = false; }
      });
    }
  }

  cancel(): void { this.dialogRef.close(false); }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
  }
}
