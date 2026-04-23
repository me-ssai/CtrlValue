import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe, PercentPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BudgetService, Budget } from '../../services/budget.service';
import { BudgetFormComponent } from './budget-form/budget-form.component';

@Component({
  selector: 'app-budgets',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatChipsModule,
    MatDialogModule,
    MatTooltipModule,
    DatePipe,
    CurrencyPipe,
    PercentPipe
  ],
  templateUrl: './budgets.component.html',
  styleUrl: './budgets.component.scss'
})
export class BudgetsComponent implements OnInit {
  private budgetService = inject(BudgetService);
  private snackBar = inject(MatSnackBar);
  private dialog = inject(MatDialog);

  dataSource: MatTableDataSource<Budget>;
  displayedColumns: string[] = ['category', 'periodType', 'period', 'progress', 'amount', 'spent', 'remaining', 'actions'];

  constructor() {
    this.dataSource = new MatTableDataSource();
  }

  ngOnInit(): void {
    this.loadBudgets();
  }

  loadBudgets(): void {
    this.budgetService.getActiveBudgets().subscribe({
      next: (budgets) => { this.dataSource.data = budgets; },
      error: (error) => {
        console.error('Error loading budgets:', error);
        this.showSnackBar('Error loading budgets');
      }
    });
  }

  createBudget(): void {
    const ref = this.dialog.open(BudgetFormComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: {}
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadBudgets();
    });
  }

  editBudget(id: string): void {
    const ref = this.dialog.open(BudgetFormComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: { budgetId: id }
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadBudgets();
    });
  }

  deleteBudget(id: string): void {
    if (confirm('Are you sure you want to delete this budget?')) {
      this.budgetService.deleteBudget(id).subscribe({
        next: () => {
          this.showSnackBar('Budget deleted');
          this.loadBudgets();
        },
        error: (error) => {
          console.error('Error deleting budget:', error);
          this.showSnackBar('Error deleting budget');
        }
      });
    }
  }

  getProgressBarColor(percent: number): string {
    if (percent > 100) return 'warn';
    if (percent > 80) return 'accent';
    return 'primary';
  }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
  }
}
