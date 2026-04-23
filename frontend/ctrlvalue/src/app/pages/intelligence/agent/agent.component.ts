import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatTabsModule } from '@angular/material/tabs';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSelectModule } from '@angular/material/select';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartOptions } from 'chart.js';

import {
    AgentService,
    AgentConfigDto,
    FinanceContextDto,
    AgentInsightDto,
    AgentMessageDto,
    MacroSummaryDto,
    AgentConversationDto,
    RunScenarioRequest,
    ScenarioResultDto,
    SavingsSnapshotDto,
    AgentScenarioHistoryDto
} from './agent.service';

@Component({
    selector: 'app-agent',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatTabsModule,
        MatCardModule,
        MatIconModule,
        MatButtonModule,
        MatProgressSpinnerModule,
        MatChipsModule,
        MatDividerModule,
        MatInputModule,
        MatFormFieldModule,
        MatTooltipModule,
        MatSelectModule,
        BaseChartDirective,
    ],
    templateUrl: './agent.component.html',
    styleUrl: './agent.component.scss'
})
export class AgentComponent implements OnInit {
    private agentService = inject(AgentService);
    private sanitizer = inject(DomSanitizer);

    // State
    config: AgentConfigDto | null = null;
    context: FinanceContextDto | null = null;
    insights: AgentInsightDto[] = [];
    macroSummaries: MacroSummaryDto[] = [];
    conversations: AgentConversationDto[] = [];
    messages: AgentMessageDto[] = [];

    // Active conversation
    activeConversationId: string | null = null;

    // Chat input
    chatInput = '';
    chatLoading = false;

    // Loading states
    loadingConfig = true;
    loadingContext = true; // true on init — context fetch starts as soon as config resolves
    loadingInsights = false;
    loadingMacro = false;
    refreshingInsights = false;

    // Errors
    configError: string | null = null;
    chatError: string | null = null;
    macroError: string | null = null;

    // Macro expand/collapse
    readonly macroPreviewLength = 220;
    private expandedMacroTopics = new Set<string>();

    // ── Savings History Chart ─────────────────────────────────────────────────
    savingsHistory: SavingsSnapshotDto[] = [];
    loadingSavingsHistory = false;

    savingsChartData: ChartConfiguration<'line'>['data'] = {
        labels: [],
        datasets: [
            {
                data: [],
                label: 'Savings Rate %',
                fill: true,
                tension: 0.3,
                borderColor: '#4caf50',
                backgroundColor: 'rgba(76, 175, 80, 0.1)',
                pointRadius: 4
            }
        ]
    };

    savingsChartOptions: ChartOptions<'line'> = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: { display: false },
            tooltip: {
                mode: 'index',
                intersect: false,
                callbacks: {
                    label: (ctx) => ` ${(ctx.parsed.y ?? 0).toFixed(1)}%`
                }
            }
        },
        scales: {
            y: {
                ticks: { callback: (v) => `${v}%` },
                grid: { color: 'rgba(255,255,255,0.06)' }
            },
            x: {
                grid: { display: false }
            }
        }
    };

    // ── Scenarios ─────────────────────────────────────────────────────────────
    scenarioHistory: AgentScenarioHistoryDto[] = [];
    loadingScenarioHistory = false;

    scenarioType: RunScenarioRequest['scenarioType'] = 'CutCategory';
    scenarioCategoryName = '';
    scenarioReductionPercent = 20;
    scenarioLoanName = '';
    scenarioTargetSavingsRate = 20;
    scenarioProjectionYears = 10;
    scenarioVehicleName = '';
    scenarioResult: ScenarioResultDto | null = null;
    scenarioLoading = false;
    scenarioError: string | null = null;

    ngOnInit(): void {
        this.loadConfig();
    }

    loadConfig(): void {
        this.loadingConfig = true;
        this.agentService.getConfig().subscribe({
            next: (config) => {
                this.config = config;
                this.loadingConfig = false;
                if (config.agentEnabled) {
                    this.loadContext(); // loadingContext is already true
                    this.loadInsights();
                    this.loadConversations();
                    this.loadMacroSummaries();
                    this.loadSavingsHistory();
                    this.loadScenarioHistory();
                } else {
                    this.loadingContext = false; // agent disabled — clear spinner
                }
            },
            error: () => {
                this.configError = 'Failed to load agent configuration.';
                this.loadingConfig = false;
                this.loadingContext = false;
            }
        });
    }

    // ── Context ───────────────────────────────────────────────────────────────

    loadContext(forceRefresh = false): void {
        this.loadingContext = true;
        this.agentService.getContextSummary(forceRefresh).subscribe({
            next: (ctx) => {
                this.context = ctx;
                this.loadingContext = false;
            },
            error: () => { this.loadingContext = false; }
        });
    }

    get netWorthFormatted(): string {
        if (!this.context) return '—';
        return this.formatCurrency(this.context.netWorth.total, this.context.currency);
    }

    // ── Savings History ───────────────────────────────────────────────────────

    loadSavingsHistory(): void {
        this.loadingSavingsHistory = true;
        this.agentService.getSavingsHistory(24).subscribe({
            next: (data) => {
                this.savingsHistory = data;
                this.loadingSavingsHistory = false;
                this.buildSavingsChart(data);
            },
            error: () => { this.loadingSavingsHistory = false; }
        });
    }

    private buildSavingsChart(data: SavingsSnapshotDto[]): void {
        this.savingsChartData = {
            labels: data.map(s => new Date(s.asOfDate).toLocaleDateString('en-AU', { month: 'short', year: '2-digit' })),
            datasets: [
                {
                    data: data.map(s => s.savingsRatePercent),
                    label: 'Savings Rate %',
                    fill: true,
                    tension: 0.3,
                    borderColor: '#4caf50',
                    backgroundColor: 'rgba(76, 175, 80, 0.1)',
                    pointRadius: 4
                }
            ]
        };
    }

    // ── Insights ──────────────────────────────────────────────────────────────

    loadInsights(): void {
        this.loadingInsights = true;
        this.agentService.getInsights().subscribe({
            next: (data) => {
                this.insights = data;
                this.loadingInsights = false;
            },
            error: () => { this.loadingInsights = false; }
        });
    }

    refreshInsights(): void {
        this.refreshingInsights = true;
        this.agentService.refreshInsights().subscribe({
            next: () => {
                this.loadInsights();
                this.refreshingInsights = false;
            },
            error: () => { this.refreshingInsights = false; }
        });
    }

    dismissInsight(insight: AgentInsightDto): void {
        this.agentService.dismissInsight(insight.id).subscribe({
            next: () => {
                this.insights = this.insights.filter(i => i.id !== insight.id);
            }
        });
    }

    insightSeverityClass(severity: string): string {
        switch (severity.toLowerCase()) {
            case 'alert': return 'severity-alert';
            case 'warning': return 'severity-warning';
            default: return 'severity-info';
        }
    }

    // ── Macro ─────────────────────────────────────────────────────────────────

    isMacroExpanded(topicKey: string): boolean {
        return this.expandedMacroTopics.has(topicKey);
    }

    toggleMacro(topicKey: string): void {
        if (this.expandedMacroTopics.has(topicKey)) {
            this.expandedMacroTopics.delete(topicKey);
        } else {
            this.expandedMacroTopics.add(topicKey);
        }
    }

    // Strip markdown links [text](url) → text, for the collapsed plain-text preview.
    private stripMarkdownLinks(text: string): string {
        return text.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');
    }

    // Convert markdown links [text](url) → <a> tags for the expanded view.
    renderSummary(summary: string): SafeHtml {
        const html = summary
            .replace(
                /\[([^\]]+)\]\((https?:\/\/[^)]+)\)/g,
                '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>'
            )
            .replace(/\n/g, '<br>');
        return this.sanitizer.bypassSecurityTrustHtml(html);
    }

    truncateSummary(summary: string): string {
        const plain = this.stripMarkdownLinks(summary);
        if (plain.length <= this.macroPreviewLength) return plain;
        const cut = plain.lastIndexOf(' ', this.macroPreviewLength);
        return plain.slice(0, cut > 0 ? cut : this.macroPreviewLength) + '…';
    }

    loadMacroSummaries(): void {
        this.loadingMacro = true;
        this.macroError = null;
        this.agentService.getMacroSummaries().subscribe({
            next: (data) => {
                this.macroSummaries = data;
                this.loadingMacro = false;
            },
            error: (err) => {
                if (err.status === 403) {
                    this.macroError = 'Macro insights are not enabled for your account.';
                } else if (err.status === 0 || err.status >= 500) {
                    this.macroError = 'Failed to load macro data. Check your API key in Settings → Integrations.';
                } else {
                    this.macroError = 'Failed to load macro summaries.';
                }
                this.loadingMacro = false;
            }
        });
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    loadConversations(): void {
        this.agentService.getConversations().subscribe({
            next: (data) => {
                this.conversations = data;
                if (data.length > 0) {
                    this.selectConversation(data[0]);
                }
            }
        });
    }

    selectConversation(conv: AgentConversationDto): void {
        this.activeConversationId = conv.id;
        this.agentService.getMessages(conv.id).subscribe({
            next: (msgs) => { this.messages = msgs; }
        });
    }

    newConversation(): void {
        this.activeConversationId = null;
        this.messages = [];
    }

    sendMessage(): void {
        const content = this.chatInput.trim();
        if (!content || this.chatLoading) return;

        this.chatLoading = true;
        this.chatError = null;
        this.chatInput = '';

        this.agentService.sendMessage({
            content,
            conversationId: this.activeConversationId
        }).subscribe({
            next: (response) => {
                this.activeConversationId = response.conversationId;
                this.messages = [...this.messages, response.userMessage, response.assistantMessage];
                this.chatLoading = false;
                // Update conversation list
                this.loadConversations();
            },
            error: (err) => {
                if (err.status === 403) {
                    this.chatError = 'Chat is not enabled for your account. Contact your administrator.';
                } else if (err.status === 0 || err.status >= 500) {
                    this.chatError = 'Failed to reach the AI provider. Check your API key in Settings → Integrations.';
                } else {
                    this.chatError = 'Failed to send message. Please try again.';
                }
                this.chatLoading = false;
            }
        });
    }

    onChatKeydown(event: KeyboardEvent): void {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            this.sendMessage();
        }
    }

    sourceLabel(sourceType: string | null): string {
        switch (sourceType) {
            case 'web': return 'Web';
            case 'hybrid': return 'Hybrid';
            default: return 'Internal';
        }
    }

    sourceClass(sourceType: string | null): string {
        switch (sourceType) {
            case 'web': return 'source-web';
            case 'hybrid': return 'source-hybrid';
            default: return 'source-internal';
        }
    }

    // ── Scenarios ─────────────────────────────────────────────────────────────

    loadScenarioHistory(): void {
        this.loadingScenarioHistory = true;
        this.agentService.getScenarioHistory(10).subscribe({
            next: (data) => { this.scenarioHistory = data; this.loadingScenarioHistory = false; },
            error: () => { this.loadingScenarioHistory = false; }
        });
    }

    runScenario(): void {
        this.scenarioLoading = true;
        this.scenarioError = null;
        this.scenarioResult = null;

        const request: RunScenarioRequest = { scenarioType: this.scenarioType };

        switch (this.scenarioType) {
            case 'CutCategory':
                request.categoryName = this.scenarioCategoryName;
                request.reductionPercent = this.scenarioReductionPercent;
                break;
            case 'PayOffLoan':
                request.loanName = this.scenarioLoanName;
                break;
            case 'IncreaseSavingsRate':
                request.targetSavingsRatePercent = this.scenarioTargetSavingsRate;
                request.projectionYears = this.scenarioProjectionYears;
                break;
            case 'SellVehicle':
                request.vehicleName = this.scenarioVehicleName;
                break;
        }

        this.agentService.runScenario(request).subscribe({
            next: (result) => {
                this.scenarioResult = result;
                this.scenarioLoading = false;
                this.loadScenarioHistory(); // refresh history
            },
            error: (err) => {
                this.scenarioError = err?.error?.error ?? 'Failed to run scenario. Check your inputs.';
                this.scenarioLoading = false;
            }
        });
    }

    get categoryOptions(): string[] {
        return this.context?.spending.topCategories.map(c => c.categoryName) ?? [];
    }

    get loanOptions(): string[] {
        return this.context?.liabilities.liabilities
            .filter(l => l.hasLoanDetails)
            .map(l => l.name) ?? [];
    }

    get vehicleOptions(): string[] {
        return this.context?.assets.costGeneratingAssetNames ?? [];
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    formatCurrency(value: number, currency = 'AUD'): string {
        return new Intl.NumberFormat('en-AU', {
            style: 'currency',
            currency,
            maximumFractionDigits: 0
        }).format(value);
    }

    formatPercent(value: number): string {
        return `${value.toFixed(1)}%`;
    }

    formatDate(dateStr: string): string {
        return new Date(dateStr).toLocaleDateString('en-AU', {
            day: 'numeric', month: 'short', year: 'numeric'
        });
    }

    objectKeys(obj: Record<string, number>): string[] {
        return Object.keys(obj);
    }
}
