import { Component, OnInit, Input, ViewChild } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar } from '@angular/material/snack-bar';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartOptions } from 'chart.js';
import { InstrumentService, Instrument } from '../../services/instrument.service';
import { PriceHistoryService, PriceHistory, BulkPriceImportRequest, PriceDataPoint } from '../../services/price-history.service';

@Component({
  selector: 'app-price-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatCardModule,
    BaseChartDirective,
    DatePipe
  ],
  templateUrl: './price-history.component.html',
  styleUrl: './price-history.component.scss'
})
export class PriceHistoryComponent implements OnInit {
  @Input() instrumentTypeFilter?: string;

  instruments: Instrument[] = [];
  selectedInstrumentId: string | null = null;
  startDate: Date = new Date(new Date().setFullYear(new Date().getFullYear() - 1)); // 1 year ago
  endDate: Date = new Date();

  displayedColumns: string[] = ['date', 'close', 'open', 'high', 'low', 'volume', 'actions'];
  dataSource: MatTableDataSource<PriceHistory>;

  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  // Chart Configuration
  public lineChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [
      {
        data: [],
        label: 'Price',
        fill: true,
        tension: 0.1,
        borderColor: '#3f51b5',
        backgroundColor: 'rgba(63, 81, 181, 0.1)'
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
      x: { display: true },
      y: { display: true }
    }
  };
  public lineChartLegend = true;

  constructor(
    private instrumentService: InstrumentService,
    private priceHistoryService: PriceHistoryService,
    private snackBar: MatSnackBar
  ) {
    this.dataSource = new MatTableDataSource();
  }

  ngOnInit(): void {
    this.loadInstruments();
  }

  loadInstruments(): void {
    this.instrumentService.getInstruments(this.instrumentTypeFilter).subscribe({
      next: (instruments) => {
        this.instruments = instruments.sort((a, b) => a.symbol.localeCompare(b.symbol));
        if (this.instruments.length > 0) {
          this.selectedInstrumentId = this.instruments[0].id;
          this.loadPriceHistory();
        }
      },
      error: (error) => console.error('Error loading instruments:', error)
    });
  }

  loadPriceHistory(): void {
    if (!this.selectedInstrumentId) return;

    const start = this.startDate.toISOString();
    const end = this.endDate.toISOString();

    this.priceHistoryService.getPriceHistory(this.selectedInstrumentId, start, end).subscribe({
      next: (history) => {
        // Sort by date descending for table
        this.dataSource.data = [...history].sort((a, b) => new Date(b.asOfDate).getTime() - new Date(a.asOfDate).getTime());
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;

        // Update Chart
        this.updateChart(history);
      },
      error: (error) => {
        console.error('Error loading price history:', error);
        this.showSnackBar('Error loading price history');
      }
    });
  }

  updateChart(history: PriceHistory[]): void {
    // Sort by date ascending for chart
    const sortedHistory = [...history].sort((a, b) => new Date(a.asOfDate).getTime() - new Date(b.asOfDate).getTime());

    this.lineChartData = {
      labels: sortedHistory.map(p => new Date(p.asOfDate).toLocaleDateString()),
      datasets: [
        {
          data: sortedHistory.map(p => p.closePrice),
          label: 'Close Price',
          fill: true,
          tension: 0.1,
          borderColor: '#3f51b5',
          backgroundColor: 'rgba(63, 81, 181, 0.1)',
          pointRadius: 2
        }
      ]
    };
  }

  onFileSelected(event: any): void {
    const file: File = event.target.files[0];
    if (file && this.selectedInstrumentId) {
      if (file.type !== 'text/csv' && !file.name.endsWith('.csv')) {
        this.showSnackBar('Please select a CSV file');
        return;
      }

      const reader = new FileReader();
      reader.onload = (e: any) => {
        const text = e.target.result;
        this.processCSV(text);
      };
      reader.readAsText(file);
    }
  }

  processCSV(csvText: string): void {
    if (!this.selectedInstrumentId) return;

    const lines = csvText.split('\n');
    const prices: PriceDataPoint[] = [];

    // Assume CSV format: Date,Close,Open,High,Low,Volume OR Date,Price
    // Simple parsing logic (can be made more robust)
    let headerSkipped = false;

    for (const line of lines) {
      if (!line.trim()) continue;

      // Simple header detection check (contains letters related to price)
      if (!headerSkipped && (line.toLowerCase().includes('date') || line.toLowerCase().includes('price'))) {
        headerSkipped = true;
        continue;
      }

      const parts = line.split(',');
      if (parts.length >= 2) {
        const dateStr = parts[0].trim();
        const price = parseFloat(parts[1].trim());

        if (!isNaN(price) && dateStr) {
          // Attempt to parse date
          const date = new Date(dateStr);
          if (!isNaN(date.getTime())) {
            prices.push({
              date: date.toISOString(),
              price: price,
              open: parts[2] ? parseFloat(parts[2]) : undefined,
              high: parts[3] ? parseFloat(parts[3]) : undefined,
              low: parts[4] ? parseFloat(parts[4]) : undefined,
              volume: parts[5] ? parseFloat(parts[5]) : undefined
            });
          }
        }
      }
    }

    if (prices.length > 0) {
      const request: BulkPriceImportRequest = {
        instrumentId: this.selectedInstrumentId,
        prices: prices
      };

      this.priceHistoryService.bulkImportPrices(request).subscribe({
        next: (response) => {
          this.showSnackBar(`Imported ${response.imported} price records`);
          this.loadPriceHistory();
        },
        error: (error) => {
          console.error('Error importing prices:', error);
          this.showSnackBar('Error importing prices');
        }
      });
    } else {
      this.showSnackBar('No valid price data found in CSV');
    }
  }

  deleteHistory(id: string): void {
    if (confirm('Delete this price record?')) {
      this.priceHistoryService.deletePriceHistory(id).subscribe({
        next: () => {
          this.showSnackBar('Price deleted successfully');
          this.loadPriceHistory();
        },
        error: (error) => console.error('Error deleting price:', error)
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
