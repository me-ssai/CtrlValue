import { Component, OnInit, Input, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PositionService, Position } from '../../services/instrument.service';
import { FinanceService } from '../../services/finance.service';
import { Account } from '../../models/api.models';
import { PositionFormComponent } from './position-form/position-form.component';
import { CurrencyPipe, PercentPipe, DatePipe } from '@angular/common';

@Component({
  selector: 'app-positions',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatDialogModule,
    MatTooltipModule,
    CurrencyPipe,
    PercentPipe,
    DatePipe
  ],
  templateUrl: './positions.component.html',
  styleUrl: './positions.component.scss'
})
export class PositionsComponent implements OnInit {
  @Input() instrumentTypeFilter?: string;
  /** When true, hides the page-header so the component can be embedded inside another page. */
  @Input() embedded = false;

  displayedColumns: string[] = ['account', 'instrument', 'quantity', 'costBasis', 'currentValue', 'gainLoss', 'actions'];
  dataSource: MatTableDataSource<Position>;
  accounts: Account[] = [];
  selectedAccountId: string | undefined;

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private positionService: PositionService,
    private financeService: FinanceService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {
    this.dataSource = new MatTableDataSource();
  }

  ngOnInit(): void {
    this.loadAccounts();
    this.loadPositions();
  }

  loadAccounts(): void {
    this.financeService.getAccounts().subscribe({
      next: (accounts) => { this.accounts = accounts; },
      error: (error) => console.error('Error loading accounts:', error)
    });
  }

  loadPositions(accountId?: string): void {
    this.positionService.getPositions(accountId).subscribe({
      next: (positions) => {
        const types = this.instrumentTypeFilter?.split('|') ?? [];
        const filtered = types.length > 0
          ? positions.filter(p => types.includes(p.instrumentType ?? ''))
          : positions;
        this.dataSource.data = filtered;
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
      },
      error: (error) => {
        console.error('Error loading positions:', error);
        this.showSnackBar('Error loading positions');
      }
    });
  }

  onAccountFilterChange(accountId: string | undefined): void {
    this.selectedAccountId = accountId;
    this.loadPositions(accountId);
  }

  applyFilter(event: Event): void {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) this.dataSource.paginator.firstPage();
  }

  createPosition(): void {
    const ref = this.dialog.open(PositionFormComponent, {
      width: '600px',
      maxWidth: '95vw',
      data: {}
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadPositions(this.selectedAccountId);
    });
  }

  editPosition(id: string): void {
    const ref = this.dialog.open(PositionFormComponent, {
      width: '600px',
      maxWidth: '95vw',
      data: { positionId: id }
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadPositions(this.selectedAccountId);
    });
  }

  deletePosition(id: string): void {
    if (confirm('Are you sure you want to delete this position?')) {
      this.positionService.deletePosition(id).subscribe({
        next: () => {
          this.showSnackBar('Position deleted successfully');
          this.loadPositions(this.selectedAccountId);
        },
        error: (error) => {
          console.error('Error deleting position:', error);
          this.showSnackBar('Error deleting position');
        }
      });
    }
  }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
  }
}
