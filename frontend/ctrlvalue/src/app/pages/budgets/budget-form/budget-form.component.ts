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
import { BudgetService } from '../../../services/budget.service';
import { FinanceService } from '../../../services/finance.service';
import { Category } from '../../../models/api.models';

export interface BudgetDialogData {
  budgetId?: string;
}

@Component({
  selector: 'app-budget-form',
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
    MatNativeDateModule
  ],
  templateUrl: './budget-form.component.html',
  styleUrl: './budget-form.component.scss'
})
export class BudgetFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private budgetService = inject(BudgetService);
  private financeService = inject(FinanceService);
  private dialogRef = inject<MatDialogRef<BudgetFormComponent>>(MatDialogRef);
  private snackBar = inject(MatSnackBar);
  data = inject<BudgetDialogData>(MAT_DIALOG_DATA);

  budgetForm: FormGroup;
  isEditMode = false;
  budgetId: string | null = null;
  categories: Category[] = [];
  loading = false;
  periodTypes = ['MONTHLY', 'QUARTERLY', 'ANNUAL'];

  constructor() {
    this.budgetForm = this.fb.group({
      categoryId: ['', Validators.required],
      periodType: ['MONTHLY', Validators.required],
      periodStart: [new Date(), Validators.required],
      periodEnd: [new Date(), Validators.required],
      amount: [null, [Validators.required, Validators.min(0)]],
      currency: ['USD', Validators.required]
    });
  }

  ngOnInit(): void {
    this.loadCategories();
    this.budgetId = this.data?.budgetId || null;
    if (this.budgetId) {
      this.isEditMode = true;
      this.loadBudget(this.budgetId);
      this.budgetForm.get('categoryId')?.disable();
      this.budgetForm.get('periodType')?.disable();
      this.budgetForm.get('periodStart')?.disable();
    } else {
      this.updateEndDate();
      this.budgetForm.get('periodStart')?.valueChanges.subscribe(() => this.updateEndDate());
      this.budgetForm.get('periodType')?.valueChanges.subscribe(() => this.updateEndDate());
    }
  }

  loadCategories(): void {
    this.financeService.getCategories('EXPENSE').subscribe({
      next: (categories) => this.categories = categories,
      error: (error) => console.error('Error loading categories:', error)
    });
  }

  loadBudget(id: string): void {
    this.budgetService.getBudgetById(id).subscribe({
      next: (budget) => {
        this.budgetForm.patchValue({
          ...budget,
          periodStart: new Date(budget.periodStart),
          periodEnd: new Date(budget.periodEnd)
        });
      },
      error: (error) => {
        console.error('Error loading budget:', error);
        this.showSnackBar('Error loading budget');
        this.dialogRef.close(false);
      }
    });
  }

  updateEndDate(): void {
    const start = this.budgetForm.get('periodStart')?.value;
    const type = this.budgetForm.get('periodType')?.value;
    if (start && type) {
      const end = new Date(start);
      if (type === 'MONTHLY') end.setMonth(end.getMonth() + 1);
      else if (type === 'QUARTERLY') end.setMonth(end.getMonth() + 3);
      else if (type === 'ANNUAL') end.setFullYear(end.getFullYear() + 1);
      end.setDate(end.getDate() - 1);
      this.budgetForm.patchValue({ periodEnd: end });
    }
  }

  onSubmit(): void {
    if (this.budgetForm.invalid) return;
    this.loading = true;
    const formValue = this.budgetForm.getRawValue();
    const request = {
      ...formValue,
      periodStart: formValue.periodStart.toISOString(),
      periodEnd: formValue.periodEnd.toISOString()
    };

    if (this.isEditMode && this.budgetId) {
      this.budgetService.updateBudget(this.budgetId, request).subscribe({
        next: () => {
          this.showSnackBar('Budget updated');
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error('Error updating budget:', error);
          this.showSnackBar('Error updating budget');
          this.loading = false;
        }
      });
    } else {
      this.budgetService.createBudget(request).subscribe({
        next: () => {
          this.showSnackBar('Budget created');
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error('Error creating budget:', error);
          this.showSnackBar('Error creating budget');
          this.loading = false;
        }
      });
    }
  }

  cancel(): void {
    this.dialogRef.close(false);
  }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
  }
}
