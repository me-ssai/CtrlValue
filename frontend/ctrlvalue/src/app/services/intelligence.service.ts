import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// DTOs — mirror of backend ITransactionIntelligenceService DTOs
// ═══════════════════════════════════════════════════════════════════════════

export interface TransferCandidate {
    outflowTxnId: string;
    outflowDescription: string;
    outflowAccount: string;
    outflowDate: string;

    inflowTxnId: string;
    inflowDescription: string;
    inflowAccount: string;
    inflowDate: string;

    amount: number;
    currency: string;
    /** 0–1 confidence score */
    confidence: number;
}

export interface RecurringPattern {
    merchantNormalised: string;
    displayName: string | null;
    typicalAmount: number;
    currency: string;
    /** "Monthly" | "Weekly" | "Fortnightly" | "Quarterly" | "Annual" | "Irregular" */
    cadence: string;
    occurrenceCount: number;
    firstSeen: string;
    lastSeen: string;
    predictedNextDate: string | null;
    transactionIds: string[];
}

export interface SpendingByMonth {
    year: number;
    month: number;
    categoryName: string;
    total: number;
}

export interface MerchantSpend {
    merchant: string;
    totalSpend: number;
    transactionCount: number;
    currency: string;
}

export interface CashFlowMonth {
    year: number;
    month: number;
    totalIncome: number;
    totalExpenses: number;
    net: number;
}

export interface ApplyRulesResult {
    categorizedCount: number;
}

export interface CategorySuggestion {
    categoryId: string;
    categoryName: string;
    matchedKeyword: string;
}

// ═══════════════════════════════════════════════════════════════════════════
// Service
// ═══════════════════════════════════════════════════════════════════════════

@Injectable({ providedIn: 'root' })
export class IntelligenceService {
    private readonly base = `${environment.apiUrl}/intelligence`;

    constructor(private http: HttpClient) {}

    // ── Transfer Detection ───────────────────────────────────────────────────

    getTransferCandidates(lookbackDays = 7): Observable<TransferCandidate[]> {
        const params = new HttpParams().set('lookbackDays', lookbackDays);
        return this.http.get<TransferCandidate[]>(`${this.base}/transfers/candidates`, { params });
    }

    linkTransfer(outflowTxnId: string, inflowTxnId: string): Observable<void> {
        return this.http.post<void>(`${this.base}/transfers/link`, { outflowTxnId, inflowTxnId });
    }

    unlinkTransfer(transferGroupId: string): Observable<void> {
        return this.http.delete<void>(`${this.base}/transfers/${transferGroupId}`);
    }

    // ── Subscriptions / Recurring ────────────────────────────────────────────

    getRecurringPatterns(lookbackMonths = 6): Observable<RecurringPattern[]> {
        const params = new HttpParams().set('lookbackMonths', lookbackMonths);
        return this.http.get<RecurringPattern[]>(`${this.base}/recurring`, { params });
    }

    // ── Spending Analytics ────────────────────────────────────────────────────

    getSpendingTrend(months = 6): Observable<SpendingByMonth[]> {
        const params = new HttpParams().set('months', months);
        return this.http.get<SpendingByMonth[]>(`${this.base}/spending/trend`, { params });
    }

    getTopMerchants(from?: Date, to?: Date, topN = 10): Observable<MerchantSpend[]> {
        let params = new HttpParams().set('topN', topN);
        if (from) params = params.set('from', from.toISOString());
        if (to)   params = params.set('to', to.toISOString());
        return this.http.get<MerchantSpend[]>(`${this.base}/spending/merchants`, { params });
    }

    getCashFlow(months = 6): Observable<CashFlowMonth[]> {
        const params = new HttpParams().set('months', months);
        return this.http.get<CashFlowMonth[]>(`${this.base}/cashflow`, { params });
    }

    // ── Categorization ────────────────────────────────────────────────────────

    applyCategorizationRules(): Observable<ApplyRulesResult> {
        return this.http.post<ApplyRulesResult>(`${this.base}/categorization/apply-rules`, {});
    }

    suggestCategory(description: string): Observable<CategorySuggestion | null> {
        const params = new HttpParams().set('description', description);
        return this.http.get<CategorySuggestion | null>(
            `${this.base}/categorization/suggest`, { params }
        ).pipe(catchError(() => of(null)));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /**
     * Builds a human-readable label for a cadence + predicted next date.
     * e.g. "Monthly · next 15 May"
     */
    formatPatternSummary(pattern: RecurringPattern): string {
        let label = pattern.cadence;
        if (pattern.predictedNextDate) {
            const d = new Date(pattern.predictedNextDate);
            const dateStr = d.toLocaleDateString('en-AU', { day: 'numeric', month: 'short' });
            label += ` · next ${dateStr}`;
        }
        return label;
    }

    /**
     * Returns the month label for a SpendingByMonth / CashFlowMonth row.
     * e.g. { year: 2025, month: 3 } → "Mar 2025"
     */
    monthLabel(year: number, month: number): string {
        return new Date(year, month - 1, 1)
            .toLocaleDateString('en-AU', { month: 'short', year: 'numeric' });
    }
}
