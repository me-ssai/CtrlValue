import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartOptions } from 'chart.js';
import { ValuationService, Valuation, UpdateValuationRequest } from '../../services/valuation.service';
import { FinanceService } from '../../services/finance.service';
import { Account } from '../../models/api.models';

@Component({
  selector: 'app-valuations',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatTableModule,
    MatCardModule,
    BaseChartDirective,
    DatePipe
  ],
  templateUrl: './valuations.component.html',
  styleUrl: './valuations.component.scss'
})
export class ValuationsComponent implements OnInit {
  accounts: Account[] = [];
  selectedAccountId: string | null = null;

  valuationForm: FormGroup;
  dataSource: MatTableDataSource<Valuation>;
  displayedColumns: string[] = ['date', 'value', 'notes', 'actions'];

  // Inline edit state
  editingId: string | null = null;
  editValue: number | null = null;
  editNotes: string = '';

  // Chart
  public lineChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'Account Value',
        fill: true,
        tension: 0.1,
        borderColor: '#4caf50',
        backgroundColor: 'rgba(76, 175, 80, 0.1)'
      }
    ]
  };
  public lineChartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: true },
      tooltip: { mode: 'index', intersect: false }
    },
    scales: {
      y: { beginAtZero: false } // Value usually doesn't start at 0
    }
  };

  constructor(
    private valuationService: ValuationService,
    private financeService: FinanceService,
    private fb: FormBuilder,
    private snackBar: MatSnackBar
  ) {
    this.dataSource = new MatTableDataSource();
    this.valuationForm = this.fb.group({
      accountId: ['', Validators.required],
      value: [null, [Validators.required, Validators.min(0)]],
      asOfDate: [new Date(), Validators.required],
      currency: ['USD', Validators.required],
      notes: ['']
    });
  }

  ngOnInit(): void {
    this.loadAccounts();

    // Listen to account changes in form to update chart/table
    this.valuationForm.get('accountId')?.valueChanges.subscribe(accountId => {
      this.selectedAccountId = accountId;
      if (accountId) {
        // Set currency based on account if possible
        const account = this.accounts.find(a => a.id === accountId);
        if (account) {
          this.valuationForm.patchValue({ currency: account.currency });
        }
        this.loadValuations(accountId);
      }
    });
  }

  loadAccounts(): void {
    this.financeService.getAccounts().subscribe({
      next: (accounts) => {
        this.accounts = accounts;
        if (this.accounts.length > 0) {
          this.valuationForm.patchValue({ accountId: this.accounts[0].id });
        }
      },
      error: (error) => console.error('Error loading accounts:', error)
    });
  }

  loadValuations(accountId: string): void {
    // Get last year of data by default
    const endDate = new Date().toISOString();
    const startDate = new Date(new Date().setFullYear(new Date().getFullYear() - 5)).toISOString(); // 5 years

    this.valuationService.getValuations(accountId, startDate, endDate).subscribe({
      next: (valuations) => {
        // Sort for table (desc)
        this.dataSource.data = [...valuations].sort((a, b) => new Date(b.asOfDate).getTime() - new Date(a.asOfDate).getTime());

        // Update Chart
        this.updateChart(valuations);
      },
      error: (error) => console.error('Error loading valuations:', error)
    });
  }

  updateChart(valuations: Valuation[]): void {
    const sorted = [...valuations].sort((a, b) => new Date(a.asOfDate).getTime() - new Date(b.asOfDate).getTime());

    this.lineChartData = {
      labels: sorted.map(v => new Date(v.asOfDate).toLocaleDateString()),
      datasets: [
        {
          data: sorted.map(v => v.value),
          label: 'Value',
          fill: true,
          tension: 0.1,
          borderColor: '#4caf50',
          backgroundColor: 'rgba(76, 175, 80, 0.1)',
          pointRadius: 4
        }
      ]
    };
  }

  onSubmit(): void {
    if (this.valuationForm.invalid) return;

    const formValue = this.valuationForm.value;
    const request = {
      ...formValue,
      asOfDate: formValue.asOfDate.toISOString()
    };

    this.valuationService.createValuation(request).subscribe({
      next: () => {
        this.showSnackBar('Valuation recorded successfully');
        this.valuationForm.get('value')?.reset();
        this.valuationForm.get('notes')?.reset();
        this.valuationForm.get('asOfDate')?.setValue(new Date());

        if (this.selectedAccountId) {
          this.loadValuations(this.selectedAccountId);
        }
      },
      error: (error) => {
        console.error('Error recording valuation:', error);
        this.showSnackBar('Error recording valuation');
      }
    });
  }

  startEdit(valuation: Valuation): void {
    this.editingId = valuation.id;
    this.editValue = valuation.value;
    this.editNotes = valuation.notes ?? '';
  }

  cancelEdit(): void {
    this.editingId = null;
    this.editValue = null;
    this.editNotes = '';
  }

  saveEdit(valuation: Valuation): void {
    if (this.editValue === null) return;
    const request: UpdateValuationRequest = {
      value: this.editValue,
      notes: this.editNotes || undefined
    };
    this.valuationService.updateValuation(valuation.id, request).subscribe({
      next: (updated) => {
        const idx = this.dataSource.data.findIndex(v => v.id === valuation.id);
        if (idx >= 0) this.dataSource.data[idx] = updated;
        this.dataSource.data = [...this.dataSource.data];
        this.updateChart(this.dataSource.data);
        this.cancelEdit();
        this.showSnackBar('Valuation updated');
      },
      error: () => this.showSnackBar('Failed to update valuation')
    });
  }

  deleteValuation(id: string): void {
    if (confirm('Delete this valuation record?')) {
      this.valuationService.deleteValuation(id).subscribe({
        next: () => {
          this.showSnackBar('Valuation deleted');
          if (this.selectedAccountId) this.loadValuations(this.selectedAccountId);
        },
        error: (error) => console.error('Error deleting valuation:', error)
      });
    }
  }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'bottom'
    });
  }
}
