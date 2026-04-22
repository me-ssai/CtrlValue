import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';

import {
    IntegrationService,
    EntityIntegration,
    FinancialConnection,
    ConnectedAccount,
    ConnectionHealth
} from '../../../services/integration.service';
import { FinanceService } from '../../../services/finance.service';
import { AccountDto } from '../../../services/api.generated';

@Component({
    selector: 'app-integrations',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatCardModule,
        MatButtonModule,
        MatIconModule,
        MatSlideToggleModule,
        MatInputModule,
        MatFormFieldModule,
        MatSnackBarModule,
        MatProgressSpinnerModule,
        MatChipsModule,
        MatDividerModule,
        MatSelectModule,
        MatTooltipModule,
        MatBadgeModule
    ],
    templateUrl: './integrations.component.html',
    styleUrl: './integrations.component.scss'
})
export class IntegrationsComponent implements OnInit {

    // ── Market data integrations ───────────────────────────────────────────
    integrations: EntityIntegration[] = [];
    integrationsLoading = false;

    avApiKey = '';
    avEnabled = false;
    avSaving = false;
    avShowKey = false;

    metalsApiKey = '';
    metalsEnabled = false;
    metalsSaving = false;
    metalsShowKey = false;

    // ── Unified connections ────────────────────────────────────────────────
    connections: FinancialConnection[] = [];
    connectedAccounts: ConnectedAccount[] = [];
    connectionHealth: ConnectionHealth[] = [];
    systemAccounts: AccountDto[] = [];

    connectionsLoading = false;
    syncing: Record<string, boolean> = {};

    // Link-account inline form state
    linkingAccountId: string | null = null;
    selectedSystemAccountId = '';

    constructor(
        private integrationService: IntegrationService,
        private financeService: FinanceService,
        private snackBar: MatSnackBar
    ) {}

    ngOnInit(): void {
        this.loadIntegrations();
        this.loadSystemAccounts();
        this.loadConnections();
    }

    // ── Market Data ─────────────────────────────────────────────────────────

    loadIntegrations(): void {
        this.integrationsLoading = true;
        this.integrationService.getIntegrations().subscribe({
            next: integrations => {
                this.integrations = integrations;

                const av = integrations.find(i => i.integrationType === 'ALPHA_VANTAGE');
                if (av) {
                    this.avEnabled = av.isEnabled;
                    this.avApiKey  = av.hasApiKey ? '••••••••••••••••' : '';
                }

                const metals = integrations.find(i => i.integrationType === 'METALS_API');
                if (metals) {
                    this.metalsEnabled = metals.isEnabled;
                    this.metalsApiKey  = metals.hasApiKey ? '••••••••••••••••' : '';
                }

                this.integrationsLoading = false;
            },
            error: () => {
                this.integrationsLoading = false;
                this.snackBar.open('Failed to load integrations', 'Close', { duration: 3000 });
            }
        });
    }

    saveAlphaVantage(): void {
        this.avSaving = true;
        const key = this.avApiKey.includes('•') ? null : this.avApiKey.trim() || null;
        this.integrationService.upsertIntegration('ALPHA_VANTAGE', { apiKey: key, isEnabled: this.avEnabled }).subscribe({
            next: () => {
                this.avSaving = false;
                this.snackBar.open('Alpha Vantage settings saved', 'Close', { duration: 2500 });
                this.loadIntegrations();
            },
            error: () => { this.avSaving = false; this.snackBar.open('Failed to save', 'Close', { duration: 3000 }); }
        });
    }

    saveMetalsApi(): void {
        this.metalsSaving = true;
        const key = this.metalsApiKey.includes('•') ? null : this.metalsApiKey.trim() || null;
        this.integrationService.upsertIntegration('METALS_API', { apiKey: key, isEnabled: this.metalsEnabled }).subscribe({
            next: () => {
                this.metalsSaving = false;
                this.snackBar.open('Metals API settings saved', 'Close', { duration: 2500 });
                this.loadIntegrations();
            },
            error: () => { this.metalsSaving = false; this.snackBar.open('Failed to save', 'Close', { duration: 3000 }); }
        });
    }

    getIntegration(type: string): EntityIntegration | undefined {
        return this.integrations.find(i => i.integrationType === type);
    }

    // ── Unified Connections ──────────────────────────────────────────────────

    loadConnections(): void {
        this.connectionsLoading = true;
        this.integrationService.getConnections().subscribe({
            next: connections => {
                this.connections = connections;
                this.connectionsLoading = false;
                this.loadConnectedAccounts();
                this.loadConnectionHealth();
            },
            error: () => {
                this.connectionsLoading = false;
            }
        });
    }

    loadConnectedAccounts(): void {
        this.integrationService.getConnectedAccounts().subscribe({
            next: accounts => this.connectedAccounts = accounts,
            error: () => {}
        });
    }

    loadConnectionHealth(): void {
        this.integrationService.getConnectionHealth().subscribe({
            next: health => this.connectionHealth = health,
            error: () => {}
        });
    }

    loadSystemAccounts(): void {
        this.financeService.getAccounts().subscribe({
            next: a => this.systemAccounts = a,
            error: () => {}
        });
    }

    /** Initiates a Manual/CSV bank connection. */
    connectBank(): void {
        this.connectionsLoading = true;
        this.integrationService.initiateConnection().subscribe({
            next: result => {
                this.connectionsLoading = false;
                // Manual/CSV always returns type="none" — complete immediately
                this.completeConnection('');
            },
            error: () => {
                this.connectionsLoading = false;
                this.snackBar.open('Could not initialise bank connection', 'Close', { duration: 3000 });
            }
        });
    }

    private completeConnection(payload: string): void {
        this.integrationService.completeConnection(payload).subscribe({
            next: conn => {
                if (!this.connections.some(c => c.id === conn.id)) {
                    this.connections.push(conn);
                }
                this.snackBar.open(`${conn.institutionName} connected`, 'Close', { duration: 3000 });
                this.loadConnectedAccounts();
                this.loadConnectionHealth();
            },
            error: () => this.snackBar.open('Failed to connect bank', 'Close', { duration: 3000 })
        });
    }

    syncConnection(connectionId: string): void {
        this.syncing[connectionId] = true;
        this.integrationService.syncConnection(connectionId).subscribe({
            next: result => {
                this.syncing[connectionId] = false;
                this.snackBar.open(
                    `Sync complete — ${result.transactionsStaged} transaction(s) staged for review`,
                    'Close', { duration: 4000 }
                );
                this.loadConnections();
            },
            error: () => {
                this.syncing[connectionId] = false;
                this.snackBar.open('Sync failed', 'Close', { duration: 3000 });
            }
        });
    }

    removeConnection(connectionId: string, institutionName: string): void {
        if (!confirm(`Disconnect ${institutionName}? This will remove all linked accounts but won't delete imported transactions.`)) return;
        this.integrationService.removeConnection(connectionId).subscribe({
            next: () => {
                this.connections    = this.connections.filter(c => c.id !== connectionId);
                this.connectedAccounts = this.connectedAccounts.filter(a => a.connectionId !== connectionId);
                this.connectionHealth  = this.connectionHealth.filter(h => h.connectionId !== connectionId);
                this.snackBar.open(`${institutionName} disconnected`, 'Close', { duration: 2500 });
            },
            error: () => this.snackBar.open('Failed to disconnect', 'Close', { duration: 3000 })
        });
    }

    accountsForConnection(connectionId: string): ConnectedAccount[] {
        return this.connectedAccounts.filter(a => a.connectionId === connectionId);
    }

    healthForConnection(connectionId: string): ConnectionHealth | undefined {
        return this.connectionHealth.find(h => h.connectionId === connectionId);
    }

    startLinkAccount(connectedAccountId: string): void {
        this.linkingAccountId = connectedAccountId;
        this.selectedSystemAccountId = '';
    }

    cancelLinkAccount(): void {
        this.linkingAccountId = null;
    }

    confirmLinkAccount(): void {
        if (!this.linkingAccountId || !this.selectedSystemAccountId) return;
        this.integrationService.linkAccount(this.linkingAccountId, this.selectedSystemAccountId).subscribe({
            next: () => {
                const ca = this.connectedAccounts.find(a => a.id === this.linkingAccountId);
                if (ca) ca.linkedAccountId = this.selectedSystemAccountId;
                this.linkingAccountId = null;
                this.snackBar.open('Account linked', 'Close', { duration: 2500 });
            },
            error: () => this.snackBar.open('Failed to link account', 'Close', { duration: 3000 })
        });
    }

    getLinkedAccountName(linkedAccountId: string | null): string {
        if (!linkedAccountId) return '';
        return this.systemAccounts.find(a => a.id === linkedAccountId)?.name ?? linkedAccountId;
    }

    // ── View helpers ─────────────────────────────────────────────────────────

    accountId(a: AccountDto): string { return a.id ?? ''; }
    accountName(a: AccountDto): string { return a.name ?? ''; }

    getStatusChipClass(status: string): string {
        switch (status) {
            case 'Active':      return 'chip-success';
            case 'NeedsReauth': return 'chip-warning';
            case 'Error':
            case 'Expired':
            case 'Disconnected': return 'chip-error';
            default:             return '';
        }
    }

    getHealthIcon(healthStatus: string): string {
        switch (healthStatus) {
            case 'Healthy':     return 'check_circle';
            case 'NeedsReauth': return 'warning';
            case 'Error':
            case 'Expired':     return 'error';
            default:            return 'help_outline';
        }
    }

    getHealthIconClass(healthStatus: string): string {
        switch (healthStatus) {
            case 'Healthy':     return 'health-ok';
            case 'NeedsReauth': return 'health-warn';
            default:            return 'health-error';
        }
    }

    formatBalance(balance: number | null, currency: string): string {
        if (balance === null) return '—';
        return new Intl.NumberFormat('en-AU', { style: 'currency', currency }).format(balance);
    }

    get hasConnectionsNeedingAttention(): boolean {
        return this.connectionHealth.some(h => h.healthStatus !== 'Healthy');
    }

    get attentionCount(): number {
        return this.connectionHealth.filter(h => h.healthStatus !== 'Healthy').length;
    }
}
