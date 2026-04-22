import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface AgentConfigDto {
    agentEnabled: boolean;
    sectionFlags: Record<string, boolean>;
    activeProvider: string | null;
}

export interface FinanceContextDto {
    entityId: string;
    asOf: string;
    currency: string;
    netWorth: NetWorthSummaryDto;
    cash: CashPositionDto;
    spending: SpendingBehaviourDto;
    saving: SavingBehaviourDto;
    liabilities: LiabilityPositionDto;
    investments: InvestmentPositionDto;
    assets: AssetEfficiencyDto;
}

export interface NetWorthSummaryDto {
    total: number;
    totalAssets: number;
    totalLiabilities: number;
    byAssetClass: Record<string, number>;
}

export interface CashPositionDto {
    totalCashBalance: number;
    estimatedIdleCash: number;
    emergencyFundEstimate: number;
    monthlyCashSurplusDeficit: number;
    cashAccounts: { name: string; institution: string; balance: number; assetClass: string }[];
}

export interface SpendingBehaviourDto {
    monthlyAverageTotal: number;
    essentialEstimate: number;
    discretionaryEstimate: number;
    topCategories: { categoryName: string; monthlyAverage: number; last30Days: number; percentOfTotal: number }[];
    subscriptions: { merchantName: string; typicalAmount: number; cadence: string }[];
    monthlySubscriptionTotal: number;
    trendDirection: string;
}

export interface SavingBehaviourDto {
    averageMonthlyIncome: number;
    averageMonthlyExpenses: number;
    averageMonthlySavings: number;
    savingsRatePercent: number;
    consistency: string;
    monthsAnalysed: number;
}

export interface LiabilityPositionDto {
    totalDebt: number;
    totalMonthlyRepayments: number;
    debtToIncomeRatio: number;
    liabilities: { name: string; institution: string; balance: number; monthlyRepayment: number; interestRate: number; hasLoanDetails: boolean }[];
}

export interface InvestmentPositionDto {
    totalValue: number;
    byAssetClass: Record<string, number>;
    hasConcentrationRisk: boolean;
    largestConcentration: string | null;
}

export interface AssetEfficiencyDto {
    incomeProducingAssetValue: number;
    costGeneratingAssetValue: number;
    vehicleCount: number;
    costGeneratingAssetNames: string[];
}

export interface AgentInsightDto {
    id: string;
    insightType: string;
    severity: string;
    title: string;
    summary: string;
    evidence: string | null;
    sourceType: string;
    isDismissed: boolean;
    generatedAt: string;
}

export interface SendMessageRequest {
    content: string;
    conversationId?: string | null;
}

export interface AgentMessageDto {
    id: string;
    role: string;
    content: string;
    structuredPayload: string | null;
    inputTokens: number | null;
    outputTokens: number | null;
    sourceType: string | null;
    createdAt: string;
}

export interface AgentChatResponse {
    conversationId: string;
    userMessage: AgentMessageDto;
    assistantMessage: AgentMessageDto;
}

export interface AgentConversationDto {
    id: string;
    title: string;
    provider: string;
    modelName: string;
    sectionType: string;
    createdAt: string;
    updatedAt: string | null;
    messageCount: number;
}

export interface MacroSummaryDto {
    topicKey: string;
    topicDisplayName: string;
    summary: string;
    sources: string | null;
    validUntil: string;
    fetchedAt: string;
    isCached: boolean;
    providerModel: string | null;
}

export interface AgentScenarioHistoryDto {
    id: string;
    scenarioType: string;
    title: string;
    result: ScenarioResultDto;
    createdAt: string;
}

export interface AgentDigestEmailDto {
    id: string;
    userId: string;
    entityId: string;
    subject: string;
    htmlBody: string;
    status: string;
    weekKey: string;
    approvedAt: string | null;
    sentAt: string | null;
    createdAt: string;
}

// ── Savings History ────────────────────────────────────────────────────────

export interface SavingsSnapshotDto {
    id: string;
    asOfDate: string;
    savingsRatePercent: number;
    averageMonthlyIncome: number;
    averageMonthlyExpenses: number;
    averageMonthlySavings: number;
    currency: string;
}

// ── Scenarios ──────────────────────────────────────────────────────────────

export interface RunScenarioRequest {
    scenarioType: 'CutCategory' | 'PayOffLoan' | 'IncreaseSavingsRate' | 'SellVehicle';
    categoryName?: string;
    reductionPercent?: number;
    loanName?: string;
    targetSavingsRatePercent?: number;
    projectionYears?: number;
    vehicleName?: string;
}

export interface ScenarioMetricDto {
    label: string;
    value: string;
    unit: string | null;
    isHighlight: boolean;
}

export interface ScenarioResultDto {
    scenarioType: string;
    title: string;
    summary: string;
    metrics: ScenarioMetricDto[];
    disclaimer: string | null;
}

export interface AgentFeatureFlagDto {
    id: string;
    key: string;
    name: string;
    description: string | null;
    isEnabled: boolean;
    sectionKey: string;
}

export interface UserFeatureFlagOverrideDto {
    assignmentId: string;
    flagKey: string;
    flagName: string;
    isEnabled: boolean;
}

export interface AgentAuditLogDto {
    id: string;
    conversationId: string | null;
    userId: string;
    requestType: string;
    provider: string;
    model: string;
    safetyDecision: string | null;
    totalTokens: number | null;
    createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class AgentService {
    private readonly api = environment.apiUrl;

    constructor(private http: HttpClient) {}

    // ── Config ────────────────────────────────────────────────────────────────
    getConfig(): Observable<AgentConfigDto> {
        return this.http.get<AgentConfigDto>(`${this.api}/agent/config`);
    }

    // ── Context ───────────────────────────────────────────────────────────────
    getContextSummary(forceRefresh = false): Observable<FinanceContextDto> {
        return this.http.get<FinanceContextDto>(`${this.api}/agent/context/summary`, {
            params: forceRefresh ? { forceRefresh: 'true' } : {}
        });
    }

    // ── Chat ──────────────────────────────────────────────────────────────────
    sendMessage(request: SendMessageRequest): Observable<AgentChatResponse> {
        return this.http.post<AgentChatResponse>(`${this.api}/agent/chat`, request);
    }

    getConversations(): Observable<AgentConversationDto[]> {
        return this.http.get<AgentConversationDto[]>(`${this.api}/agent/conversations`);
    }

    getMessages(conversationId: string): Observable<AgentMessageDto[]> {
        return this.http.get<AgentMessageDto[]>(`${this.api}/agent/conversations/${conversationId}/messages`);
    }

    // ── Insights ──────────────────────────────────────────────────────────────
    getInsights(): Observable<AgentInsightDto[]> {
        return this.http.get<AgentInsightDto[]>(`${this.api}/agent/insights`);
    }

    refreshInsights(): Observable<void> {
        return this.http.post<void>(`${this.api}/agent/insights/refresh`, {});
    }

    dismissInsight(insightId: string): Observable<void> {
        return this.http.post<void>(`${this.api}/agent/insights/${insightId}/dismiss`, {});
    }

    // ── Scenarios ─────────────────────────────────────────────────────────────
    runScenario(request: RunScenarioRequest): Observable<ScenarioResultDto> {
        return this.http.post<ScenarioResultDto>(`${this.api}/agent/scenarios/run`, request);
    }

    // ── Macro ─────────────────────────────────────────────────────────────────
    getMacroSummaries(): Observable<MacroSummaryDto[]> {
        return this.http.get<MacroSummaryDto[]>(`${this.api}/agent/macro`);
    }

    getMacroSummary(topicKey: string): Observable<MacroSummaryDto> {
        return this.http.get<MacroSummaryDto>(`${this.api}/agent/macro/${topicKey}`);
    }

    // ── Admin ─────────────────────────────────────────────────────────────────
    adminGetFlags(): Observable<AgentFeatureFlagDto[]> {
        return this.http.get<AgentFeatureFlagDto[]>(`${this.api}/admin/agent/features`);
    }

    adminUpdateFlag(sectionKey: string, isEnabled: boolean): Observable<AgentFeatureFlagDto> {
        return this.http.put<AgentFeatureFlagDto>(`${this.api}/admin/agent/features/${sectionKey}`, { isEnabled });
    }

    adminGetUserOverrides(userId: string): Observable<UserFeatureFlagOverrideDto[]> {
        return this.http.get<UserFeatureFlagOverrideDto[]>(`${this.api}/admin/agent/users/${userId}/overrides`);
    }

    adminSetUserOverride(userId: string, sectionKey: string, isEnabled: boolean): Observable<void> {
        return this.http.put<void>(`${this.api}/admin/agent/users/${userId}/overrides/${sectionKey}`, { isEnabled });
    }

    adminRemoveUserOverride(userId: string, sectionKey: string): Observable<void> {
        return this.http.delete<void>(`${this.api}/admin/agent/users/${userId}/overrides/${sectionKey}`);
    }

    adminGetAuditLogs(userId?: string, page = 1, pageSize = 50): Observable<AgentAuditLogDto[]> {
        const params: Record<string, string> = { page: String(page), pageSize: String(pageSize) };
        if (userId) params['userId'] = userId;
        return this.http.get<AgentAuditLogDto[]>(`${this.api}/admin/agent/audit`, { params });
    }

    // ── Admin: Digest Approval ────────────────────────────────────────────────
    adminGetPendingDigests(): Observable<AgentDigestEmailDto[]> {
        return this.http.get<AgentDigestEmailDto[]>(`${this.api}/admin/agent/digests/pending`);
    }

    adminGetAllDigests(page = 1, pageSize = 50): Observable<AgentDigestEmailDto[]> {
        return this.http.get<AgentDigestEmailDto[]>(`${this.api}/admin/agent/digests`, {
            params: { page: String(page), pageSize: String(pageSize) }
        });
    }

    adminApproveDigest(digestId: string): Observable<void> {
        return this.http.post<void>(`${this.api}/admin/agent/digests/${digestId}/approve`, {});
    }

    adminRejectDigest(digestId: string): Observable<void> {
        return this.http.post<void>(`${this.api}/admin/agent/digests/${digestId}/reject`, {});
    }

    // ── Scenario History ──────────────────────────────────────────────────────
    getScenarioHistory(limit = 20): Observable<AgentScenarioHistoryDto[]> {
        return this.http.get<AgentScenarioHistoryDto[]>(`${this.api}/agent/scenarios`, {
            params: { limit: String(limit) }
        });
    }

    // ── Savings History ───────────────────────────────────────────────────────
    getSavingsHistory(months = 24): Observable<SavingsSnapshotDto[]> {
        return this.http.get<SavingsSnapshotDto[]>(`${this.api}/agent/savings-history`, {
            params: { months: String(months) }
        });
    }
}
