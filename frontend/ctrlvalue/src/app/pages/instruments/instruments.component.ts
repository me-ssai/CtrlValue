import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { InstrumentService, Instrument } from '../../services/instrument.service';
import { InstrumentFormComponent } from './instrument-form/instrument-form.component';
import { CurrencyPipe } from '@angular/common';

@Component({
  selector: 'app-instruments',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatButtonModule,
    MatChipsModule,
    MatDialogModule,
    MatTooltipModule,
    CurrencyPipe
  ],
  templateUrl: './instruments.component.html',
  styleUrl: './instruments.component.scss'
})
export class InstrumentsComponent implements OnInit {
  /** When set, only instruments of this type are loaded (e.g. 'METAL'). */
  @Input() typeFilter?: string;
  /** Set to false when embedded in a parent page that supplies its own header/actions. */
  @Input() showHeader = true;

  displayedColumns: string[] = ['symbol', 'name', 'type', 'currency', 'exchange', 'latestPrice', 'actions'];
  dataSource: MatTableDataSource<Instrument>;

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  constructor(
    private instrumentService: InstrumentService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {
    this.dataSource = new MatTableDataSource();
  }

  ngOnInit(): void {
    this.loadInstruments();
  }

  loadInstruments(): void {
    // Fetch all instruments, then filter client-side to support multi-type filters (e.g. 'ETF|FUND')
    this.instrumentService.getInstruments().subscribe({
      next: (instruments) => {
        const types = this.typeFilter?.split('|') ?? [];
        this.dataSource.data = types.length > 0
          ? instruments.filter(i => types.includes(i.instrumentType))
          : instruments;
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
      },
      error: (error) => {
        console.error('Error loading instruments:', error);
        this.showSnackBar('Error loading instruments');
      }
    });
  }

  applyFilter(event: Event): void {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) this.dataSource.paginator.firstPage();
  }

  createInstrument(): void {
    const ref = this.dialog.open(InstrumentFormComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: {}
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadInstruments();
    });
  }

  editInstrument(id: string): void {
    const ref = this.dialog.open(InstrumentFormComponent, {
      width: '560px',
      maxWidth: '95vw',
      data: { instrumentId: id }
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadInstruments();
    });
  }

  deleteInstrument(id: string): void {
    if (confirm('Are you sure you want to delete this instrument? This may affect existing positions.')) {
      this.instrumentService.deleteInstrument(id).subscribe({
        next: () => {
          this.showSnackBar('Instrument deleted successfully');
          this.loadInstruments();
        },
        error: (error) => {
          console.error('Error deleting instrument:', error);
          this.showSnackBar('Error deleting instrument');
        }
      });
    }
  }

  private showSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', { duration: 3000, horizontalPosition: 'end', verticalPosition: 'bottom' });
  }
}
