import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatGridListModule } from '@angular/material/grid-list';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatMenuModule } from '@angular/material/menu';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { AuthService } from '../../services/auth.service';
import { DashboardSummary } from '../../models/api.models';
import { LoanService, LoanSummaryDto, AmortisationScheduleDto, AmortisationRowDto } from '../../services/loan.service';
import { FinanceService } from '../../services/finance.service';
import { IntelligenceService, CashFlowMonth, RecurringPattern, TransferCandidate } from '../../services/intelligence.service';
import { environment } from '@env/environment';
import { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';

@Component({
    selector: 'app-dashboard',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        FormsModule,
        MatCardModule,
        MatGridListModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatTabsModule,
        MatTableModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatTooltipModule,
        MatChipsModule,
        MatDividerModule,
        MatPaginatorModule,
        BaseChartDirective,
        MatMenuModule,
        MatDialogModule
    ],
    providers: [provideCharts(withDefaultRegisterables())],
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
    summary: DashboardSummary | null = null;
    loading = true;
    refreshingBalances = false;

    // Loans tab
    loanSummaries: LoanSummaryDto[] = [];
    loansLoading = true;
    /** Per-loan amortisation schedule keyed by accountId */
    schedules: Record<string, AmortisationScheduleDto> = {};
    scheduleLoading: Record<string, boolean> = {};
    scheduleColumns = ['paymentNumber', 'paymentDate', 'paymentAmount', 'principal', 'interest', 'cumulativeInterest', 'balance'];
    /** Extra payment input per loan (keyed by accountId) */
    extraPayments: Record<string, number> = {};
    expandedSchedule: Record<string, boolean> = {};
    /** Cached chart data per loan, rebuilt when schedule loads/refreshes */
    balanceChartDataCache: Record<string, ChartData<'line'>> = {};

    // ── Intelligence ─────────────────────────────────────────────────────
    cashFlow: CashFlowMonth[] = [];
    cashFlowLoading = false;
    recurringPatterns: RecurringPattern[] = [];
    recurringLoading = false;
    transferCandidates: TransferCandidate[] = [];
    transferCandidatesLoading = false;
    linkingTransfer: TransferCandidate | null = null;

    fabOpen = false;

    constructor(
        public authService: AuthService,
        private http: HttpClient,
        public router: Router,
        private loanService: LoanService,
        private financeService: FinanceService,
        private intelligenceService: IntelligenceService,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadDashboard();
        this.loadEntityAndLoans();
        this.loadIntelligence();
    }

    private loadIntelligence(): void {
        // Cash flow
        this.cashFlowLoading = true;
        this.intelligenceService.getCashFlow(6).subscribe({
            next: cf => { this.cashFlow = cf; this.cashFlowLoading = false; },
            error: ()  => { this.cashFlowLoading = false; }
        });

        // Recurring / subscriptions
        this.recurringLoading = true;
        this.intelligenceService.getRecurringPatterns(6).subscribe({
            next: p => { this.recurringPatterns = p; this.recurringLoading = false; },
            error: () => { this.recurringLoading = false; }
        });

        // Transfer candidates
        this.transferCandidatesLoading = true;
        this.intelligenceService.getTransferCandidates(14).subscribe({
            next: c => { this.transferCandidates = c; this.transferCandidatesLoading = false; },
            error: () => { this.transferCandidatesLoading = false; }
        });
    }

    confirmLinkTransfer(candidate: TransferCandidate): void {
        this.intelligenceService.linkTransfer(candidate.outflowTxnId, candidate.inflowTxnId).subscribe({
            next: () => {
                this.transferCandidates = this.transferCandidates.filter(
                    c => c.outflowTxnId !== candidate.outflowTxnId);
                this.linkingTransfer = null;
            },
            error: () => {}
        });
    }

    dismissTransferCandidate(candidate: TransferCandidate): void {
        this.transferCandidates = this.transferCandidates.filter(c => c !== candidate);
    }

    get monthlyRecurringTotal(): number {
        return this.recurringPatterns
            .filter(p => p.cadence === 'Monthly')
            .reduce((sum, p) => sum + p.typicalAmount, 0);
    }

    formatMonth(year: number, month: number): string {
        return this.intelligenceService.monthLabel(year, month);
    }

    formatPatternSummary(pattern: RecurringPattern): string {
        return this.intelligenceService.formatPatternSummary(pattern);
    }

    private loadEntityAndLoans(): void {
        // Get the user's entity from the auth context then load loans
        this.http.get<any[]>(`${environment.apiUrl}/entities`).subscribe({
            next: (entities) => {
                if (entities.length > 0) {
                    this.loadLoans(entities[0].id);
                } else {
                    this.loansLoading = false;
                }
            },
            error: () => { this.loansLoading = false; }
        });
    }

    private loadLoans(entityId: string): void {
        this.loansLoading = true;
        // Get all LIABILITY accounts, then fetch summary for each that has loan details
        this.financeService.getAccounts('LIABILITY').subscribe({
            next: (accounts) => {
                if (accounts.length === 0) { this.loansLoading = false; return; }
                const requests = accounts.length;
                let done = 0;
                const summaries: LoanSummaryDto[] = [];
                accounts.forEach(acc => {
                    this.loanService.getLoanSummary(acc.id!).subscribe({
                        next: (s) => {
                            summaries.push(s);
                            this.extraPayments[acc.id!] = 0;
                            if (++done === requests) { this.loanSummaries = summaries; this.loansLoading = false; }
                        },
                        error: () => { if (++done === requests) { this.loanSummaries = summaries; this.loansLoading = false; } }
                    });
                });
            },
            error: () => { this.loansLoading = false; }
        });
    }

    loadSchedule(accountId: string): void {
        if (this.schedules[accountId]) { this.expandedSchedule[accountId] = !this.expandedSchedule[accountId]; return; }
        this.scheduleLoading[accountId] = true;
        const extra = this.extraPayments[accountId] ?? 0;
        this.loanService.getAmortisationSchedule(accountId, extra).subscribe({
            next: (s) => {
                this.schedules[accountId] = s;
                this.balanceChartDataCache[accountId] = this.buildBalanceChartData(accountId);
                this.scheduleLoading[accountId] = false;
                this.expandedSchedule[accountId] = true;
            },
            error: () => { this.scheduleLoading[accountId] = false; }
        });
    }

    refreshSchedule(accountId: string): void {
        delete this.schedules[accountId];
        delete this.balanceChartDataCache[accountId];
        this.loadSchedule(accountId);
    }

    private transactionMeta: Record<string, { cssClass: string }> = {
        Income: { cssClass: 'income' },
        AssetSale: { cssClass: 'income' },
        LoanDisbursement: { cssClass: 'income' },
        CapitalDeposit: { cssClass: 'income' },
        OpeningBalance: { cssClass: 'neutral' },
        Expense: { cssClass: 'expense' },
        AssetPurchase: { cssClass: 'expense' },
        LoanRepayment: { cssClass: 'expense' },
        LoanInterestCharge: { cssClass: 'expense' },
        CapitalWithdrawal: { cssClass: 'expense' },
        Transfer: { cssClass: 'transfer' }
    };

    quickAddAccount(): void {
        import('../accounts/account-form/account-form.component').then(m => {
            const ref = this.dialog.open(m.AccountFormComponent, { width: '600px', maxWidth: '95vw' });
            ref.afterClosed().subscribe(result => { if (result) this.loadDashboard(); });
        });
    }

    quickAddTransaction(): void {
        import('../transactions/transaction-form/transaction-form.component').then(m => {
            const ref = this.dialog.open(m.TransactionFormComponent, {
                width: '720px', maxWidth: '95vw',
                data: {}
            });
            ref.afterClosed().subscribe(result => { if (result) this.loadDashboard(); });
        });
    }

    loadDashboard(): void {
        this.http.get<DashboardSummary>(`${environment.apiUrl}/dashboard/summary`).subscribe({
            next: (data) => {
                this.summary = data;
                this.loading = false;
                this.buildAllocationChartData();
            },
            error: () => {
                this.loading = false;
                // Show zeroes for new users with no data
                this.summary = {
                    totalAssets: 0,
                    totalLiabilities: 0,
                    netWorth: 0,
                    assetCount: 0,
                    liabilityCount: 0,
                    transactionCountThisMonth: 0,
                    incomeThisMonth: 0,
                    expensesThisMonth: 0,
                    recentTransactions: [],
                    holdings: []
                };
            }
        });
    }

    refreshBalances(): void {
        this.refreshingBalances = true;
        this.http.post(`${environment.apiUrl}/accounts/recalculate-all`, {}).subscribe({
            next: () => {
                this.loadDashboard();
                this.refreshingBalances = false;
            },
            error: (err) => {
                console.error('Failed to recalculate balances', err);
                this.refreshingBalances = false;
            }
        });
    }

    getHoldingValue(type: string | "", value: number | undefined | null): number {

        if (type == "ASSET") {
            return value ?? 0;
        }
        else if (type == "LIABILITY") {
            return -(value ?? 0);
        }
        else {
            return 0;
        }

    }

    getTransactionIcon(type: string): string {
        const iconMap: Record<string, string> = {
            Income: 'arrow_downward',
            Expense: 'arrow_upward',
            Transfer: 'swap_horiz',
            AssetPurchase: 'shopping_cart',
            AssetSale: 'sell',
            LoanDisbursement: 'account_balance',
            LoanRepayment: 'payments',
            CapitalDeposit: 'account_balance_wallet',
            CapitalWithdrawal: 'money_off'
        };

        return iconMap[type] ?? 'help_outline';
    }

    getTransactionSign(type?: string): string {
        const positiveTypes = [
            'Income',
            'AssetSale',
            'LoanDisbursement',
            'CapitalDeposit'
        ];

        const negativeTypes = [
            'Expense',
            'AssetPurchase',
            'LoanRepayment',
            'CapitalWithdrawal'
        ];

        if (!type) return '';

        if (positiveTypes.includes(type)) return '+';
        if (negativeTypes.includes(type)) return '-';

        return ''; // Transfer or unknown
    }

    get sortedHoldings() {
        return [...(this.summary?.holdings ?? [])].sort((a, b) =>
            (a.accountName ?? '').localeCompare(b.accountName ?? '')
        );
    }

    get assetHoldings() {
        return this.sortedHoldings.filter(h => h.accountType === 'ASSET');
    }

    get liabilityHoldings() {
        return this.sortedHoldings.filter(h => h.accountType === 'LIABILITY');
    }

    get liquidityBreakdown(): Array<{
        key: string; label: string; icon: string; colorClass: string;
        total: number; percentage: number;
    }> {
        const tiers = [
            { key: 'LIQUID',      label: 'Liquid',      icon: 'water_drop', colorClass: 'liq-liquid'      },
            { key: 'SEMI_LIQUID', label: 'Semi-Liquid', icon: 'opacity',    colorClass: 'liq-semi-liquid'  },
            { key: 'ILLIQUID',    label: 'Illiquid',    icon: 'home',       colorClass: 'liq-illiquid'     },
            { key: 'LOCKED',      label: 'Locked',      icon: 'lock',       colorClass: 'liq-locked'       },
        ];
        const totalAssets = this.summary?.totalAssets ?? 0;
        return tiers.map(tier => {
            const total = this.assetHoldings
                .filter(h => (h.liquidityClass ?? 'LIQUID') === tier.key)
                .reduce((sum, h) => sum + (h.value ?? 0), 0);
            const percentage = totalAssets > 0 ? (total / totalAssets) * 100 : 0;
            return { ...tier, total, percentage };
        });
    }

    get superTotal(): number {
        return this.assetHoldings
            .filter(h => h.assetClass === 'SUPER')
            .reduce((sum, h) => sum + (h.value ?? 0), 0);
    }

    get netWorthExclSuper(): number {
        return (this.summary?.netWorth ?? 0) - this.superTotal;
    }

    get assetsExclSuper(): number {
        return (this.summary?.totalAssets ?? 0) - this.superTotal;
    }

    getTransactionCssClass(type?: string): string {
        return this.transactionMeta[type ?? '']?.cssClass ?? 'neutral';
    }

    formatCurrency(value: number): string {
        return new Intl.NumberFormat('en-AU', {
            style: 'currency',
            currency: 'AUD',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value);
    }

    lvrClass(lvr?: number): string {
        if (!lvr) return 'lvr-ok';
        if (lvr < 0.80) return 'lvr-ok';
        if (lvr <= 0.90) return 'lvr-warn';
        return 'lvr-danger';
    }

    formatRate(rate?: number): string {
        if (!rate) return '—';
        return (rate * 100).toFixed(2) + '%';
    }

    get holdingsColumns(): string[] {
        return ['accountName', 'institution', 'accountType', 'value'];
    }

    getScheduleDataSource(accountId: string): MatTableDataSource<AmortisationRowDto> {
        const rows = this.schedules[accountId]?.standard ?? [];
        return new MatTableDataSource(rows);
    }

    getAcceleratedDataSource(accountId: string): MatTableDataSource<AmortisationRowDto> {
        const rows = this.schedules[accountId]?.accelerated ?? [];
        return new MatTableDataSource(rows);
    }

    navigateToAccount(id: string | undefined): void {
        if (id) this.router.navigate(['/accounts', id]);
    }

    // ── Loan balance payoff chart ──────────────────────────────────────────────

    balanceChartOptions: ChartOptions<'line'> = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: { position: 'top' },
            tooltip: { mode: 'index', intersect: false }
        },
        scales: {
            y: {
                ticks: { callback: (v) => '$' + Number(v).toLocaleString('en-AU') }
            }
        }
    };

    private buildBalanceChartData(accountId: string): ChartData<'line'> {
        const schedule = this.schedules[accountId];
        if (!schedule) return { labels: [], datasets: [] };

        const step = Math.max(1, Math.floor(schedule.standard.length / 60));
        const sample = (rows: AmortisationRowDto[]) =>
            rows.filter((_, i) => i % step === 0 || i === rows.length - 1);

        const stdRows = sample(schedule.standard);
        return {
            labels: stdRows.map(r => new Date(r.paymentDate).toLocaleDateString('en-AU', { year: 'numeric', month: 'short' })),
            datasets: [
                {
                    label: 'Standard',
                    data: stdRows.map(r => r.balance),
                    borderColor: '#5b9bd5',
                    backgroundColor: 'rgba(91,155,213,0.1)',
                    fill: true,
                    tension: 0.3,
                    pointRadius: 0
                },
                ...(schedule.extraPaymentPerPeriod > 0 ? [{
                    label: `With Extra ${this.formatCurrency(schedule.extraPaymentPerPeriod)}/period`,
                    data: sample(schedule.accelerated).map(r => r.balance),
                    borderColor: '#70ad47',
                    backgroundColor: 'rgba(112,173,71,0.1)',
                    fill: true,
                    tension: 0.3,
                    pointRadius: 0
                }] : [])
            ]
        };
    }

    // ── Asset allocation doughnut chart ───────────────────────────────────────

    allocationChartOptions: ChartOptions<'doughnut'> = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: { position: 'right' },
            tooltip: {
                callbacks: {
                    label: (ctx) => `${ctx.label}: ${this.formatCurrency(ctx.parsed)}`
                }
            }
        }
    };

    allocationChartData: ChartData<'doughnut'> = { labels: [], datasets: [] };

    private buildAllocationChartData(): void {
        const grouped: Record<string, number> = {};
        for (const h of this.assetHoldings) {
            const key = h.assetClass ?? 'Other';
            grouped[key] = (grouped[key] ?? 0) + (h.value ?? 0);
        }
        const palette = ['#5b9bd5','#70ad47','#ffc000','#ed7d31','#a5a5a5','#4472c4','#9dc3e6','#c5e0b4','#ffe699','#f4b183'];
        const labels = Object.keys(grouped);
        this.allocationChartData = {
            labels,
            datasets: [{
                data: labels.map(l => grouped[l]),
                backgroundColor: labels.map((_, i) => palette[i % palette.length]),
                hoverOffset: 6
            }]
        };
    }

    // ── Export amortisation to CSV ────────────────────────────────────────────

    exportScheduleToCsv(accountId: string): void {
        const schedule = this.schedules[accountId];
        if (!schedule) return;
        const loan = this.loanSummaries.find(l => l.accountId === accountId);
        const rows = schedule.standard;
        const header = ['#', 'Date', 'Payment', 'Principal', 'Interest', 'Cumulative Interest', 'Balance'];
        const lines = [
            header.join(','),
            ...rows.map(r => [
                r.paymentNumber,
                new Date(r.paymentDate).toLocaleDateString('en-AU'),
                r.paymentAmount.toFixed(2),
                r.principal.toFixed(2),
                r.interest.toFixed(2),
                r.cumulativeInterest.toFixed(2),
                r.balance.toFixed(2)
            ].join(','))
        ];
        const blob = new Blob([lines.join('\n')], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `amortisation-${loan?.accountName ?? accountId}.csv`;
        a.click();
        URL.revokeObjectURL(url);
    }
}
