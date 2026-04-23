import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, CurrencyPipe, PercentPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';
import { forkJoin } from 'rxjs';
import { PositionService, Position } from '../../../services/instrument.service';
import { PropertyService, Property } from '../../../services/property.service';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../models/api.models';

interface HoldingRow {
    label: string;
    category: string;
    categoryRoute: string;
    quantity: string;
    value: number;
    costBasis: number;
    gainLoss: number;
    gainLossPercent: number;
    weight: number;
    currency: string;
}

const CATEGORY_LABELS: Record<string, string> = {
    STOCK: 'Stocks', ETF: 'ETFs', FUND: 'ETFs', BOND: 'Bonds',
    METAL: 'Metals', CRYPTO: 'Crypto',
    PROPERTY: 'Property', SUPER: 'Super', BUSINESS: 'Other', VEHICLE: 'Other', OTHER: 'Other'
};

const CATEGORY_ROUTES: Record<string, string> = {
    STOCK: '/investing/stocks', ETF: '/investing/etfs', FUND: '/investing/etfs',
    BOND: '/investing/bonds', METAL: '/investing/metals', CRYPTO: '/investing/crypto',
    PROPERTY: '/investing/real-estate', SUPER: '/investing/super',
    BUSINESS: '/investing/other', VEHICLE: '/investing/other', OTHER: '/investing/other'
};

const CHART_COLORS = [
    '#1565c0', '#2e7d32', '#f57f17', '#6a1b9a', '#00838f',
    '#ad1457', '#558b2f', '#4e342e', '#546e7a', '#e65100'
];

@Component({
    selector: 'app-investment-hub',
    standalone: true,
    imports: [
        CommonModule, CurrencyPipe, PercentPipe, RouterModule,
        MatIconModule, MatButtonModule, MatCardModule, MatTableModule,
        MatChipsModule, MatTooltipModule, MatProgressSpinnerModule,
        BaseChartDirective
    ],
    template: `
        <div class="page-container">
            <div class="page-header">
                <h1><mat-icon>donut_large</mat-icon> Portfolio</h1>
                <button mat-stroked-button (click)="loadAll()">
                    <mat-icon>refresh</mat-icon> Refresh
                </button>
            </div>

            <div *ngIf="loading" class="d-flex justify-content-center py-5">
                <mat-spinner diameter="48"></mat-spinner>
            </div>

            <ng-container *ngIf="!loading">

                <!-- ── Summary strip ── -->
                <div class="d-flex gap-3 mb-4 flex-wrap">
                    <mat-card class="summary-card">
                        <mat-card-content>
                            <div class="summary-label">Portfolio (excl. Super)</div>
                            <div class="summary-value">{{ investableValue | currency:'AUD' }}</div>
                        </mat-card-content>
                    </mat-card>
                    <mat-card class="summary-card super-card" *ngIf="superValue > 0">
                        <mat-card-content>
                            <div class="summary-label">Super</div>
                            <div class="summary-value">{{ superValue | currency:'AUD' }}</div>
                        </mat-card-content>
                    </mat-card>
                    <mat-card class="summary-card">
                        <mat-card-content>
                            <div class="summary-label">Cost Basis</div>
                            <div class="summary-value">{{ totalCost | currency:'AUD' }}</div>
                        </mat-card-content>
                    </mat-card>
                    <mat-card class="summary-card" [class.gain-card]="totalGainLoss > 0" [class.loss-card]="totalGainLoss < 0">
                        <mat-card-content>
                            <div class="summary-label">Total Gain / Loss</div>
                            <div class="summary-value">{{ totalGainLoss | currency:'AUD' }}</div>
                        </mat-card-content>
                    </mat-card>
                    <mat-card class="summary-card" [class.gain-card]="totalGainLoss > 0" [class.loss-card]="totalGainLoss < 0">
                        <mat-card-content>
                            <div class="summary-label">Return</div>
                            <div class="summary-value">{{ totalGainLossPercent / 100 | percent:'1.1-1' }}</div>
                        </mat-card-content>
                    </mat-card>
                    <mat-card class="summary-card">
                        <mat-card-content>
                            <div class="summary-label">Holdings</div>
                            <div class="summary-value">{{ allHoldings.length }}</div>
                        </mat-card-content>
                    </mat-card>
                </div>

                <!-- ── Charts row ── -->
                <div class="row g-3 mb-4" *ngIf="allHoldings.length > 0">
                    <div class="col-md-6">
                        <mat-card class="chart-card h-100">
                            <mat-card-header>
                                <mat-card-title>Asset Allocation</mat-card-title>
                            </mat-card-header>
                            <mat-card-content class="chart-content">
                                <canvas baseChart
                                    [data]="donutData"
                                    [options]="donutOptions"
                                    type="doughnut">
                                </canvas>
                            </mat-card-content>
                        </mat-card>
                    </div>
                    <div class="col-md-6">
                        <mat-card class="chart-card h-100">
                            <mat-card-header>
                                <mat-card-title>Category Breakdown</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                <div class="category-list">
                                    <div *ngFor="let cat of categoryBreakdown; let i = index"
                                        class="category-row"
                                        [routerLink]="cat.route"
                                        style="cursor:pointer">
                                        <div class="cat-dot" [style.background]="CHART_COLORS[i % CHART_COLORS.length]"></div>
                                        <div class="cat-name">{{ cat.name }}</div>
                                        <div class="cat-bar-wrap">
                                            <div class="cat-bar" [style.width.%]="cat.percent"
                                                [style.background]="CHART_COLORS[i % CHART_COLORS.length]"></div>
                                        </div>
                                        <div class="cat-value">{{ cat.value | currency:'AUD':'symbol':'1.0-0' }}</div>
                                        <div class="cat-pct text-muted">{{ cat.percent | number:'1.0-0' }}%</div>
                                    </div>
                                </div>
                            </mat-card-content>
                        </mat-card>
                    </div>
                </div>

                <!-- ── Holdings table ── -->
                <mat-card>
                    <mat-card-header>
                        <mat-card-title>All Holdings</mat-card-title>
                        <div class="ms-auto d-flex gap-2 flex-wrap">
                            <button *ngFor="let f of categoryFilters" mat-stroked-button
                                [color]="activeFilter === f ? 'primary' : ''"
                                (click)="setFilter(f)">{{ f }}</button>
                        </div>
                    </mat-card-header>
                    <mat-card-content class="p-0">
                        <div class="table-container">
                            <table mat-table [dataSource]="tableDataSource" class="w-100">
                                <ng-container matColumnDef="asset">
                                    <th mat-header-cell *matHeaderCellDef>Asset</th>
                                    <td mat-cell *matCellDef="let row">
                                        <a [routerLink]="row.categoryRoute" class="link-primary fw-semibold">{{ row.label }}</a>
                                        <div class="subtitle text-muted">{{ row.quantity }}</div>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="category">
                                    <th mat-header-cell *matHeaderCellDef>Category</th>
                                    <td mat-cell *matCellDef="let row">
                                        <mat-chip>{{ row.category }}</mat-chip>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="value">
                                    <th mat-header-cell *matHeaderCellDef>Value</th>
                                    <td mat-cell *matCellDef="let row" class="fw-semibold">
                                        {{ row.value | currency:row.currency }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="costBasis">
                                    <th mat-header-cell *matHeaderCellDef>Cost Basis</th>
                                    <td mat-cell *matCellDef="let row">
                                        {{ row.costBasis > 0 ? (row.costBasis | currency:row.currency) : '—' }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="gainLoss">
                                    <th mat-header-cell *matHeaderCellDef>Gain / Loss</th>
                                    <td mat-cell *matCellDef="let row"
                                        [class.gain]="row.gainLoss > 0" [class.loss]="row.gainLoss < 0">
                                        <ng-container *ngIf="row.costBasis > 0">
                                            {{ row.gainLoss | currency:row.currency }}
                                            <div class="subtitle">{{ row.gainLossPercent / 100 | percent:'1.1-1' }}</div>
                                        </ng-container>
                                        <span *ngIf="row.costBasis === 0" class="text-muted">—</span>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="weight">
                                    <th mat-header-cell *matHeaderCellDef>Weight</th>
                                    <td mat-cell *matCellDef="let row" class="text-muted">
                                        {{ row.weight / 100 | percent:'1.1-1' }}
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="displayedColumns; sticky: true"></tr>
                                <tr mat-row *matRowDef="let row; columns: displayedColumns;"
                                    [routerLink]="row.categoryRoute" style="cursor:pointer"></tr>
                                <tr class="mat-row" *matNoDataRow>
                                    <td class="mat-cell empty-state" colspan="6">
                                        No holdings found. Add investments to get started.
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </mat-card-content>
                </mat-card>

            </ng-container>
        </div>
    `,
    styles: [`
        .summary-card { min-width: 150px; flex: 1; background: var(--color-bg-card); border: 1px solid var(--color-border); border-radius: var(--radius-lg); }
        .summary-label { font-family: var(--font-family-display, 'Barlow Condensed', sans-serif); font-size: 0.65rem; color: var(--color-text-muted, #666); text-transform: uppercase; letter-spacing: 0.10em; margin-bottom: 4px; font-weight: 600; }
        .summary-value { font-family: var(--font-family-mono, 'IBM Plex Mono', monospace); font-size: 1.3rem; font-weight: 500; color: var(--color-text-primary); }
        .gain-card .summary-value { color: var(--color-accent-success); }
        .loss-card .summary-value { color: var(--color-accent-danger); }
        .super-card .summary-value { color: var(--color-accent-warning); }
        .gain { color: var(--color-accent-success); font-family: var(--font-family-mono, monospace); }
        .loss { color: var(--color-accent-danger); font-family: var(--font-family-mono, monospace); }
        .subtitle { font-size: 0.72rem; color: var(--color-text-muted); }
        .chart-card { background: var(--color-bg-card); border: 1px solid var(--color-border); border-radius: var(--radius-lg); }
        .chart-content { height: 260px; display: flex; align-items: center; justify-content: center; }
        .category-list { display: flex; flex-direction: column; gap: 8px; padding: 8px 0; }
        .category-row { display: flex; align-items: center; gap: 10px; font-size: 0.82rem; }
        .cat-dot { width: 10px; height: 10px; border-radius: 2px; flex-shrink: 0; }
        .cat-name { min-width: 80px; color: var(--color-text-secondary); font-size: 0.8rem; }
        .cat-bar-wrap { flex: 1; height: 4px; background: var(--color-border); border-radius: 2px; overflow: hidden; }
        .cat-bar { height: 100%; border-radius: 2px; transition: width 0.4s; }
        .cat-value { min-width: 90px; text-align: right; font-family: var(--font-family-mono, monospace); font-size: 0.78rem; font-weight: 500; color: var(--color-text-primary); }
        .cat-pct { min-width: 36px; text-align: right; color: var(--color-text-muted); font-size: 0.72rem; }
    `]
})
export class InvestmentHubComponent implements OnInit {
    private positionService = inject(PositionService);
    private propertyService = inject(PropertyService);
    private financeService = inject(FinanceService);

    readonly CHART_COLORS = CHART_COLORS;

    loading = true;
    displayedColumns = ['asset', 'category', 'value', 'costBasis', 'gainLoss', 'weight'];
    categoryFilters = ['All', 'Stocks', 'ETFs', 'Bonds', 'Metals', 'Crypto', 'Property', 'Other'];
    activeFilter = 'All';

    allHoldings: HoldingRow[] = [];
    tableDataSource = new MatTableDataSource<HoldingRow>();
    totalValue = 0;
    totalCost = 0;
    totalGainLoss = 0;
    totalGainLossPercent = 0;
    superValue = 0;
    investableValue = 0;

    categoryBreakdown: { name: string; value: number; percent: number; route: string }[] = [];

    donutData: ChartConfiguration<'doughnut'>['data'] = { labels: [], datasets: [{ data: [], backgroundColor: [] }] };
    donutOptions: ChartConfiguration<'doughnut'>['options'] = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: { position: 'right' },
            tooltip: {
                callbacks: {
                    label: (ctx) => ` $${(ctx.raw as number).toLocaleString('en-AU', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
                }
            }
        }
    };

    ngOnInit(): void { this.loadAll(); }

    loadAll(): void {
        this.loading = true;
        forkJoin({
            positions: this.positionService.getPositions(),
            properties: this.propertyService.getProperties(),
            accounts: this.financeService.getAccounts()
        }).subscribe({
            next: ({ positions, properties, accounts }) => {
                this.buildHoldings(positions, properties, accounts);
                this.loading = false;
            },
            error: () => { this.loading = false; }
        });
    }

    private buildHoldings(positions: Position[], properties: Property[], accounts: AccountDto[]): void {
        const rows: HoldingRow[] = [];

        // Compute super value directly from accounts (not included in the holdings table)
        this.superValue = accounts
            .filter(a => a.assetClass === 'SUPER')
            .reduce((s, a) => s + (a.currentBalance ?? 0), 0);

        // Positions (stocks, ETFs, bonds, metals, crypto)
        for (const p of positions) {
            const type = p.instrumentType || 'OTHER';
            const value = p.currentValue ?? 0;
            // When cost basis is 0 or null, fall back to current value → shows 0 gain/loss
            const cost = p.costBasisTotal || value;
            const gl = p.unrealizedGainLoss ?? (value - cost);
            rows.push({
                label: p.instrumentName ? `${p.instrumentSymbol} — ${p.instrumentName}` : (p.instrumentSymbol || 'Unknown'),
                category: CATEGORY_LABELS[type] || 'Other',
                categoryRoute: CATEGORY_ROUTES[type] || '/investing',
                quantity: `${p.quantity} ${p.unit}`,
                value,
                costBasis: cost,
                gainLoss: gl,
                gainLossPercent: p.unrealizedGainLossPercent ?? (cost > 0 ? (gl / cost) * 100 : 0),
                weight: 0,
                currency: 'AUD'
            });
        }

        // Properties
        for (const prop of properties) {
            const value = prop.currentValue ?? 0;
            const cost = prop.purchasePrice ?? 0;
            const gl = value - cost;
            rows.push({
                label: prop.address,
                category: 'Property',
                categoryRoute: '/investing/real-estate',
                quantity: prop.propertyType,
                value,
                costBasis: cost,
                gainLoss: gl,
                gainLossPercent: cost > 0 ? (gl / cost) * 100 : 0,
                weight: 0,
                currency: 'AUD'
            });
        }

        // Other accounts (business, vehicle, other) — SUPER excluded from holdings table
        const otherClasses = new Set(['BUSINESS', 'VEHICLE', 'OTHER']);
        for (const acc of accounts) {
            if (!otherClasses.has(acc.assetClass ?? '')) continue;
            const value = acc.currentBalance ?? 0;
            const cat = CATEGORY_LABELS[acc.assetClass ?? ''] || 'Other';
            rows.push({
                label: acc.name ?? '',
                category: cat,
                categoryRoute: CATEGORY_ROUTES[acc.assetClass ?? ''] || '/investing/other',
                quantity: acc.assetClass ?? '',
                value,
                // Use current value as cost basis when not entered → shows 0 gain/loss
                costBasis: value,
                gainLoss: 0,
                gainLossPercent: 0,
                weight: 0,
                currency: acc.currency ?? 'AUD'
            });
        }

        // Calculate totals & weights (super excluded from totalValue — shown separately in summary)
        this.totalValue = rows.reduce((s, r) => s + r.value, 0);
        this.totalCost = rows.reduce((s, r) => s + r.costBasis, 0);
        this.totalGainLoss = this.totalValue - this.totalCost;
        this.totalGainLossPercent = this.totalCost > 0 ? (this.totalGainLoss / this.totalCost) * 100 : 0;
        this.investableValue = this.totalValue;

        for (const r of rows) {
            r.weight = this.totalValue > 0 ? (r.value / this.totalValue) * 100 : 0;
        }

        this.allHoldings = rows;
        this.buildCategoryBreakdown(rows);
        this.setFilter(this.activeFilter);
    }

    private buildCategoryBreakdown(rows: HoldingRow[]): void {
        const map = new Map<string, { value: number; route: string }>();
        for (const r of rows) {
            const existing = map.get(r.category);
            if (existing) { existing.value += r.value; }
            else { map.set(r.category, { value: r.value, route: r.categoryRoute }); }
        }

        const breakdown = Array.from(map.entries())
            .map(([name, { value, route }]) => ({
                name, value, route,
                percent: this.totalValue > 0 ? (value / this.totalValue) * 100 : 0
            }))
            .sort((a, b) => b.value - a.value);

        this.categoryBreakdown = breakdown;

        this.donutData = {
            labels: breakdown.map(b => b.name),
            datasets: [{
                data: breakdown.map(b => b.value),
                backgroundColor: CHART_COLORS.slice(0, breakdown.length),
                hoverOffset: 6
            }]
        };
    }

    setFilter(filter: string): void {
        this.activeFilter = filter;
        const filtered = filter === 'All'
            ? this.allHoldings
            : this.allHoldings.filter(r => r.category === filter);
        this.tableDataSource.data = filtered.sort((a, b) => b.value - a.value);
    }
}
