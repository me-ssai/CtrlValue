import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe, PercentPipe } from '@angular/common';
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
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DepreciationScheduleService, DepreciationSchedule } from '../../services/valuation.service'; // In valuation.service.ts
import { FinanceService } from '../../services/finance.service';
import { Account } from '../../models/api.models';

@Component({
  selector: 'app-depreciation-schedules',
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
    MatExpansionModule,
    DatePipe,
    CurrencyPipe,
    PercentPipe
  ],
  templateUrl: './depreciation-schedules.component.html',
  styleUrl: './depreciation-schedules.component.scss'
})
export class DepreciationSchedulesComponent implements OnInit {
  private depreciationService = inject(DepreciationScheduleService);
  private financeService = inject(FinanceService);
  private fb = inject(FormBuilder);
  private snackBar = inject(MatSnackBar);

  accounts: Account[] = [];
  scheduleForm: FormGroup;
  dataSource: MatTableDataSource<DepreciationSchedule>;
  displayedColumns: string[] = ['account', 'method', 'purchasePrice', 'currentValue', 'bookValue', 'actions'];

  methods = ['STRAIGHT_LINE', 'DECLINING_BALANCE', 'REDBOOK'];
  loading = false;

  constructor() {
    this.dataSource = new MatTableDataSource();
    this.scheduleForm = this.fb.group({
      accountId: ['', Validators.required],
      method: ['STRAIGHT_LINE', Validators.required],
      purchasePrice: [null, [Validators.required, Validators.min(0)]],
      purchaseDate: [new Date(), Validators.required],
      usefulLifeYears: [null, [Validators.min(0)]],
      salvageValue: [0, [Validators.min(0)]],
      annualDepreciationRate: [null, [Validators.min(0), Validators.max(100)]]
    });
  }

  ngOnInit(): void {
    this.loadAccounts();
    this.loadSchedules();
  }

  loadAccounts(): void {
    this.financeService.getAccounts().subscribe({
      next: (accounts) => this.accounts = accounts,
      error: (error) => console.error('Error loading accounts:', error)
    });
  }

  loadSchedules(): void {
    this.depreciationService.getDepreciationSchedules().subscribe({
      next: (schedules) => {
        this.dataSource.data = schedules;
      },
      error: (error) => console.error('Error loading schedules:', error)
    });
  }

  onSubmit(): void {
    if (this.scheduleForm.invalid) return;

    this.loading = true;
    const formValue = this.scheduleForm.value;
    const request = {
      ...formValue,
      purchaseDate: formValue.purchaseDate.toISOString()
    };

    this.depreciationService.createDepreciationSchedule(request).subscribe({
      next: () => {
        this.showSnackBar('Schedule created successfully');
        this.scheduleForm.reset({
          method: 'STRAIGHT_LINE',
          purchaseDate: new Date(),
          salvageValue: 0
        });
        this.loadSchedules();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error creating schedule:', error);
        this.showSnackBar('Error creating schedule');
        this.loading = false;
      }
    });
  }

  deleteSchedule(id: string): void {
    if (confirm('Delete this depreciation schedule?')) {
      this.depreciationService.deleteDepreciationSchedule(id).subscribe({
        next: () => {
          this.showSnackBar('Schedule deleted');
          this.loadSchedules();
        },
        error: (error) => console.error('Error deleting schedule:', error)
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
