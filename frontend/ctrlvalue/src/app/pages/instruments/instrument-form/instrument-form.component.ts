import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, filter } from 'rxjs/operators';
import { InstrumentService, InstrumentSearchResult } from '../../../services/instrument.service';

export interface InstrumentDialogData {
  instrumentId?: string;
  defaultType?: string;
  defaultPriceProvider?: string;
}

// Map instrument type → default price provider
const DEFAULT_PROVIDER_MAP: Record<string, string> = {
  STOCK:  'ALPHA_VANTAGE',
  ETF:    'ALPHA_VANTAGE',
  BOND:   'ALPHA_VANTAGE',
  METAL:  'METALS_API',
  CRYPTO: 'COINGECKO',
};

interface Market {
  label: string;
  suffix: string;        // Alpha Vantage exchange suffix (confirmed from live API)
  currency: string;      // Default currency for this exchange
  exchange: string;      // Exchange name stored on the instrument
  searchable: boolean;   // false = Alpha Vantage SYMBOL_SEARCH doesn't support this exchange
}

const MARKETS: Market[] = [
  { label: 'Global',        suffix: '',    currency: 'USD', exchange: 'GLOBAL', searchable: true },
  { label: 'NYSE / NASDAQ', suffix: '',    currency: 'USD', exchange: 'NYSE',   searchable: true },
  { label: 'ASX',           suffix: '.AX', currency: 'AUD', exchange: 'ASX',   searchable: true },
];

@Component({
  selector: 'app-instrument-form',
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
    MatAutocompleteModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './instrument-form.component.html',
  styleUrl: './instrument-form.component.scss'
})
export class InstrumentFormComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private instrumentService = inject(InstrumentService);
  private dialogRef = inject<MatDialogRef<InstrumentFormComponent>>(MatDialogRef);
  private snackBar = inject(MatSnackBar);
  data = inject<InstrumentDialogData>(MAT_DIALOG_DATA);

  instrumentForm: FormGroup;
  isEditMode = false;
  instrumentId: string | null = null;
  loading = false;

  // Autocomplete state
  searchResults: InstrumentSearchResult[] = [];
  searching = false;

  // Market selector (create mode only)
  readonly markets: Market[] = MARKETS;
  selectedMarket: Market = MARKETS[0];

  instrumentTypes = ['STOCK', 'BOND', 'ETF', 'METAL', 'CRYPTO', 'FUND', 'OTHER'];
  priceProviders = [
    { value: 'ALPHA_VANTAGE',  label: 'Alpha Vantage (US Stocks / ETFs / Bonds)' },
    { value: 'YAHOO_FINANCE',  label: 'Yahoo Finance (ASX / International)' },
    { value: 'METALS_API',     label: 'Metals API (Gold / Silver / Platinum)' },
    { value: 'COINGECKO',      label: 'CoinGecko (Crypto)' },
    { value: 'MANUAL',         label: 'Manual entry only' },
  ];
  metalUnits = ['UNIT', 'TROY_OZ', 'GRAM', 'KILOGRAM', 'TOLA'];
  currencies = ['USD', 'EUR', 'GBP', 'AUD', 'CAD', 'JPY', 'CHF'];

  private searchInput$ = new Subject<string>();
  private subs = new Subscription();

  constructor() {
    const data = this.data;

    this.instrumentForm = this.fb.group({
      symbol: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(20)]],
      name: ['', [Validators.required, Validators.maxLength(100)]],
      instrumentType: [data?.defaultType || 'STOCK', Validators.required],
      currency: ['USD', Validators.required],
      exchange: [''],
      externalSymbol: [''],
      priceProvider: [data?.defaultPriceProvider || ''],
      priceUnit: ['UNIT']
    });
  }

  ngOnInit(): void {
    this.instrumentId = this.data?.instrumentId || null;
    if (this.instrumentId) {
      this.isEditMode = true;
      this.loadInstrument(this.instrumentId);
      this.instrumentForm.get('symbol')?.disable();
    }

    // Wire up debounced symbol search (only in create mode)
    if (!this.isEditMode) {
      this.subs.add(
        this.searchInput$.pipe(
          debounceTime(400),
          distinctUntilChanged(),
          filter(q => q.length >= 2),
          switchMap(q => {
            if (!this.selectedMarket.searchable) {
              this.searching = false;
              return [];
            }
            this.searching = true;
            const type = this.instrumentForm.get('instrumentType')?.value;
            const exch = this.selectedMarket.exchange || undefined;
            return this.instrumentService.searchInstruments(this.buildSearchQuery(q), type, exch);
          })
        ).subscribe({
          next: results => {
            this.searchResults = results;
            this.searching = false;
          },
          error: () => {
            this.searching = false;
          }
        })
      );
    }
  }

  /** Returns the query to send to the backend search endpoint.
   *  Suffix appending is intentionally skipped — for ASX the exchange param scopes
   *  Yahoo Finance to AU; for US markets there is no suffix. The suffix is only used
   *  post-selection to strip the provider symbol back to a clean display ticker. */
  private buildSearchQuery(rawQuery: string): string {
    return rawQuery;
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  onSymbolInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchInput$.next(value);
  }

  onMarketChange(): void {
    this.searchResults = [];
    if (!this.selectedMarket.searchable) return;
    const current = this.instrumentForm.get('symbol')?.value;
    if (typeof current !== 'string' || current.length < 2) return;
    this.searching = true;
    const type = this.instrumentForm.get('instrumentType')?.value;
    this.subs.add(
      this.instrumentService.searchInstruments(this.buildSearchQuery(current), type, this.selectedMarket.exchange || undefined).subscribe({
        next: results => { this.searchResults = results; this.searching = false; },
        error: () => { this.searching = false; }
      })
    );
  }

  onSearchResultSelected(event: MatAutocompleteSelectedEvent): void {
    const result: InstrumentSearchResult = event.option.value;
    const suffix = this.selectedMarket.suffix;

    // Strip known exchange suffix from the display symbol (e.g. GOLD.AX → GOLD)
    const cleanSymbol = suffix && result.symbol.toUpperCase().endsWith(suffix.toUpperCase())
      ? result.symbol.slice(0, -suffix.length).toUpperCase()
      : result.symbol.toUpperCase();

    this.instrumentForm.patchValue({
      symbol:         cleanSymbol,
      name:           result.name,
      currency:       result.currency || this.selectedMarket.currency || 'USD',
      exchange:       result.exchange || this.selectedMarket.exchange || '',
      instrumentType: result.type || this.instrumentForm.get('instrumentType')?.value,
      priceProvider:  this.selectedMarket.exchange === 'ASX' ? 'YAHOO_FINANCE' : (DEFAULT_PROVIDER_MAP[result.type] || ''),
      externalSymbol: result.symbol.toUpperCase(),  // full symbol with suffix for price provider
    });

    this.searchResults = [];
  }

  /** Display function for mat-autocomplete: show the symbol string */
  displaySymbol(result: InstrumentSearchResult | string | null): string {
    if (!result) return '';
    return typeof result === 'string' ? result : result.symbol;
  }

  loadInstrument(id: string): void {
    this.loading = true;
    this.instrumentService.getInstrumentById(id).subscribe({
      next: (instrument) => {
        this.instrumentForm.patchValue(instrument);
        this.loading = false;
      },
      error: () => {
        this.showSnackBar('Error loading instrument details');
        this.dialogRef.close(false);
      }
    });
  }

  onSubmit(): void {
    if (this.instrumentForm.invalid) return;
    this.loading = true;
    const request = this.instrumentForm.getRawValue();

    if (this.isEditMode && this.instrumentId) {
      this.instrumentService.updateInstrument(this.instrumentId, request).subscribe({
        next: () => {
          this.showSnackBar('Instrument updated successfully');
          this.dialogRef.close(true);
        },
        error: () => {
          this.showSnackBar('Error updating instrument');
          this.loading = false;
        }
      });
    } else {
      this.instrumentService.createInstrument(request).subscribe({
        next: () => {
          this.showSnackBar('Instrument created successfully');
          this.dialogRef.close(true);
        },
        error: () => {
          this.showSnackBar('Error creating instrument');
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
