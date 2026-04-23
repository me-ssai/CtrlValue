import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PriceHistoryComponent } from '../../price-history/price-history.component';
import { MetalHoldingFormComponent } from './metal-holding-form/metal-holding-form.component';
import { PositionService, Position } from '../../../services/instrument.service';

interface MetalSummary {
    name: string;
    symbol: string;
    totalQuantity: number;
    unit: string;
    currency: string;
    costBasis: number;
    currentValue: number;
    gainLoss: number;
    gainLossPercent: number;
}

@Component({
    selector: 'app-metals',
    standalone: true,
    imports: [
        CommonModule, CurrencyPipe,
        MatTabsModule, MatIconModule, MatButtonModule, MatTableModule,
        MatCardModule, MatTooltipModule, MatDialogModule,
        PriceHistoryComponent
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>diamond</mat-icon> Precious Metals</h1>
                <button mat-raised-button color="primary" (click)="addHolding()">
                    <mat-icon>add</mat-icon>
                    Add Holding
                </button>
            </div>
            <mat-tab-group animationDuration="200ms">

                <!-- Holdings tab -->
                <mat-tab label="Holdings">
                    <ng-template matTabContent>
                        <div class="table-container mt-3">
                            <table mat-table [dataSource]="holdingsDataSource" class="w-100">
                                <ng-container matColumnDef="metal">
                                    <th mat-header-cell *matHeaderCellDef>Metal</th>
                                    <td mat-cell *matCellDef="let row">
                                        <strong>{{ row.instrumentName || row.instrumentSymbol }}</strong>
                                        <div class="subtitle text-muted">{{ row.instrumentSymbol }}</div>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="quantity">
                                    <th mat-header-cell *matHeaderCellDef>Quantity</th>
                                    <td mat-cell *matCellDef="let row">{{ row.quantity }} {{ row.unit | lowercase }}</td>
                                </ng-container>
                                <ng-container matColumnDef="costBasis">
                                    <th mat-header-cell *matHeaderCellDef>Cost Basis</th>
                                    <td mat-cell *matCellDef="let row">
                                        {{ row.costBasisTotal | currency:(row.currency || 'AUD') }}
                                        <div class="subtitle text-muted">{{ row.costBasisPerUnit | currency:(row.currency || 'AUD') }}/unit</div>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="currentValue">
                                    <th mat-header-cell *matHeaderCellDef>Current Value</th>
                                    <td mat-cell *matCellDef="let row" class="fw-semibold">
                                        {{ row.currentValue | currency:(row.currency || 'AUD') }}
                                        <div class="subtitle text-muted">{{ row.currentPrice | currency:(row.currency || 'AUD') }}/unit</div>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="gainLoss">
                                    <th mat-header-cell *matHeaderCellDef>Gain / Loss</th>
                                    <td mat-cell *matCellDef="let row"
                                        [class.gain]="(row.unrealizedGainLoss || 0) > 0"
                                        [class.loss]="(row.unrealizedGainLoss || 0) < 0">
                                        {{ row.unrealizedGainLoss | currency:(row.currency || 'AUD') }}
                                        <div class="subtitle">{{ (row.unrealizedGainLossPercent || 0) / 100 | percent:'1.1-1' }}</div>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="account">
                                    <th mat-header-cell *matHeaderCellDef>Account</th>
                                    <td mat-cell *matCellDef="let row" class="text-muted">{{ row.accountName }}</td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef>Actions</th>
                                    <td mat-cell *matCellDef="let row">
                                        <button mat-icon-button color="primary" (click)="editHolding(row.id)" matTooltip="Edit">
                                            <mat-icon>edit</mat-icon>
                                        </button>
                                        <button mat-icon-button color="warn" (click)="deleteHolding(row.id)" matTooltip="Remove">
                                            <mat-icon>delete</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="holdingColumns"></tr>
                                <tr mat-row *matRowDef="let row; columns: holdingColumns;"></tr>
                                <tr class="mat-row" *matNoDataRow>
                                    <td class="mat-cell empty-state" colspan="7">
                                        No metal holdings yet. Click "Add Holding" to get started.
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </ng-template>
                </mat-tab>

                <!-- Spot Prices tab -->
                <mat-tab label="Spot Prices">
                    <ng-template matTabContent>
                        <app-price-history instrumentTypeFilter="METAL"></app-price-history>
                    </ng-template>
                </mat-tab>

                <!-- Summary tab -->
                <mat-tab label="Summary">
                    <ng-template matTabContent>
                        <div class="pt-3">
                            <div class="d-flex gap-3 mb-4 flex-wrap">
                                <mat-card class="summary-card">
                                    <mat-card-content>
                                        <div class="summary-label">Total Value</div>
                                        <div class="summary-value">{{ totalValue | currency:summaryDisplayCurrency }}</div>
                                    </mat-card-content>
                                </mat-card>
                                <mat-card class="summary-card">
                                    <mat-card-content>
                                        <div class="summary-label">Total Cost</div>
                                        <div class="summary-value">{{ totalCost | currency:summaryDisplayCurrency }}</div>
                                    </mat-card-content>
                                </mat-card>
                                <mat-card class="summary-card" [class.gain]="totalGainLoss > 0" [class.loss]="totalGainLoss < 0">
                                    <mat-card-content>
                                        <div class="summary-label">Total Gain / Loss</div>
                                        <div class="summary-value">{{ totalGainLoss | currency:summaryDisplayCurrency }}</div>
                                    </mat-card-content>
                                </mat-card>
                            </div>

                            <div class="table-container">
                                <table mat-table [dataSource]="summaryDataSource" class="w-100">
                                    <ng-container matColumnDef="metal">
                                        <th mat-header-cell *matHeaderCellDef>Metal</th>
                                        <td mat-cell *matCellDef="let row"><strong>{{ row.name }}</strong></td>
                                    </ng-container>
                                    <ng-container matColumnDef="quantity">
                                        <th mat-header-cell *matHeaderCellDef>Total Held</th>
                                        <td mat-cell *matCellDef="let row">{{ row.totalQuantity }} {{ row.unit | lowercase }}</td>
                                    </ng-container>
                                    <ng-container matColumnDef="cost">
                                        <th mat-header-cell *matHeaderCellDef>Cost Basis</th>
                                        <td mat-cell *matCellDef="let row">{{ row.costBasis | currency:(row.currency || 'AUD') }}</td>
                                    </ng-container>
                                    <ng-container matColumnDef="value">
                                        <th mat-header-cell *matHeaderCellDef>Market Value</th>
                                        <td mat-cell *matCellDef="let row" class="fw-semibold">{{ row.currentValue | currency:(row.currency || 'AUD') }}</td>
                                    </ng-container>
                                    <ng-container matColumnDef="gainLoss">
                                        <th mat-header-cell *matHeaderCellDef>Gain / Loss</th>
                                        <td mat-cell *matCellDef="let row"
                                            [class.gain]="row.gainLoss > 0" [class.loss]="row.gainLoss < 0">
                                            {{ row.gainLoss | currency:(row.currency || 'AUD') }}
                                            <div class="subtitle">{{ row.gainLossPercent / 100 | percent:'1.1-1' }}</div>
                                        </td>
                                    </ng-container>
                                    <tr mat-header-row *matHeaderRowDef="summaryColumns"></tr>
                                    <tr mat-row *matRowDef="let row; columns: summaryColumns;"></tr>
                                </table>
                            </div>
                        </div>
                    </ng-template>
                </mat-tab>

            </mat-tab-group>
        </div>
    `,
    styles: [`
        .summary-card { min-width: 150px; }
        .summary-label { font-size: 0.75rem; color: var(--text-secondary, #666); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 4px; }
        .summary-value { font-size: 1.5rem; font-weight: 600; }
        .gain { color: #2e7d32; }
        .loss { color: #c62828; }
        .subtitle { font-size: 0.75rem; opacity: 0.7; }
    `]
})
export class MetalsComponent implements OnInit {
    private positionService = inject(PositionService);
    private dialog = inject(MatDialog);
    private snackBar = inject(MatSnackBar);

    holdingColumns = ['metal', 'quantity', 'costBasis', 'currentValue', 'gainLoss', 'account', 'actions'];
    summaryColumns = ['metal', 'quantity', 'cost', 'value', 'gainLoss'];

    holdingsDataSource = new MatTableDataSource<Position>();
    summaryDataSource = new MatTableDataSource<MetalSummary>();

    totalValue = 0;
    totalCost = 0;
    totalGainLoss = 0;
    /** Currency used for aggregate totals — taken from the first holding, falls back to AUD. */
    summaryDisplayCurrency = 'AUD';

    ngOnInit(): void {
        this.loadHoldings();
    }

    loadHoldings(): void {
        this.positionService.getPositions().subscribe({
            next: (positions) => {
                const metals = positions.filter(p => p.instrumentType === 'METAL');
                this.holdingsDataSource.data = metals;
                this.buildSummary(metals);
            },
            error: () => console.error('Error loading metal holdings')
        });
    }

    buildSummary(positions: Position[]): void {
        const byInstrument = new Map<string, Position[]>();
        for (const p of positions) {
            const key = p.instrumentSymbol || p.instrumentId || 'Unknown';
            if (!byInstrument.has(key)) byInstrument.set(key, []);
            byInstrument.get(key)!.push(p);
        }

        const summaries: MetalSummary[] = [];
        for (const [symbol, group] of byInstrument.entries()) {
            const totalQty = group.reduce((s, p) => s + p.quantity, 0);
            const cost = group.reduce((s, p) => s + (p.costBasisTotal || 0), 0);
            const value = group.reduce((s, p) => s + (p.currentValue || 0), 0);
            const currency = group[0].currency || 'AUD';
            summaries.push({
                name: group[0].instrumentName || symbol,
                symbol,
                totalQuantity: totalQty,
                unit: group[0].unit,
                currency,
                costBasis: cost,
                currentValue: value,
                gainLoss: value - cost,
                gainLossPercent: cost > 0 ? ((value - cost) / cost) * 100 : 0
            });
        }

        this.summaryDataSource.data = summaries;
        this.totalCost = positions.reduce((s, p) => s + (p.costBasisTotal || 0), 0);
        this.totalValue = positions.reduce((s, p) => s + (p.currentValue || 0), 0);
        this.totalGainLoss = this.totalValue - this.totalCost;
        this.summaryDisplayCurrency = positions[0]?.currency || 'AUD';
    }

    addHolding(): void {
        const ref = this.dialog.open(MetalHoldingFormComponent, {
            width: '560px', maxWidth: '95vw', data: {}
        });
        ref.afterClosed().subscribe(result => { if (result) this.loadHoldings(); });
    }

    editHolding(id: string): void {
        const ref = this.dialog.open(MetalHoldingFormComponent, {
            width: '560px', maxWidth: '95vw', data: { positionId: id }
        });
        ref.afterClosed().subscribe(result => { if (result) this.loadHoldings(); });
    }

    deleteHolding(id: string): void {
        if (confirm('Remove this metal holding?')) {
            this.positionService.deletePosition(id).subscribe({
                next: () => { this.snackBar.open('Holding removed', 'Close', { duration: 3000 }); this.loadHoldings(); },
                error: () => this.snackBar.open('Error removing holding', 'Close', { duration: 3000 })
            });
        }
    }
}
