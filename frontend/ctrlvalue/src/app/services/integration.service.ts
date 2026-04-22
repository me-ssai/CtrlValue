import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Unified Connection Models (provider-agnostic)
// ═══════════════════════════════════════════════════════════════════════════

export interface EntityIntegration {
    id: string;
    entityId: string;
    integrationType: string;  // 'ALPHA_VANTAGE' | 'METALS_API' | 'BASIQ'
    isEnabled: boolean;
    hasApiKey: boolean;
    lastSyncedAt: string | null;
    createdAt: string;
}

export interface UpsertIntegrationRequest {
    apiKey: string | null;
    isEnabled: boolean;
}

/** A connected bank/institution  — works for Plaid, Basiq, CSV, Manual. */
export interface FinancialConnection {
    id: string;
    entityId: string;
    /** "Manual" | "Csv" */
    provider: string;
    providerConnectionId: string;
    institutionName: string;
    institutionLogoUrl: string | null;
    /** "Active" | "NeedsReauth" | "Error" | "Expired" | "Disconnected" */
    status: string;
    statusMessage: string | null;
    country: string;
    lastSyncedAt: string | null;
    consentExpiresAt: string | null;
    accountCount: number;
}

/** A single account returned by any provider. */
export interface ConnectedAccount {
    id: string;
    connectionId: string;
    externalAccountId: string;
    name: string;
    officialName: string | null;
    /** Last 4 digits / BSB mask */
    mask: string | null;
    type: string;
    subtype: string | null;
    currentBalance: number | null;
    availableBalance: number | null;
    currencyCode: string;
    isActive: boolean;
    linkedAccountId: string | null;
    lastSyncedAt: string | null;
}

/** Result of initiating the connection flow. */
export interface ConnectionInitResult {
    /** "link_token" | "auth_url" | "none" */
    type: string;
    /** The link token (Plaid) or auth URL (Basiq), empty for Manual/CSV. */
    value: string;
}

export interface SyncResult {
    accountsSynced: boolean;
    transactionsStaged: number;
    status: string;
    errorMessage: string | null;
}

/** Real-time health for a single connection. */
export interface ConnectionHealth {
    connectionId: string;
    institutionName: string;
    provider: string;
    /** "Healthy" | "NeedsReauth" | "Error" | "Expired" */
    healthStatus: string;
    lastSyncedAt: string | null;
    consentExpiresAt: string | null;
    statusMessage: string | null;
}

// ═══════════════════════════════════════════════════════════════════════════
// Integration Service
// ═══════════════════════════════════════════════════════════════════════════

@Injectable({ providedIn: 'root' })
export class IntegrationService {
    private readonly baseUrl        = `${environment.apiUrl}/integrations`;
    private readonly connectionsUrl = `${environment.apiUrl}/connections`;

    constructor(private http: HttpClient) {}

    // ── Market Data Integrations ────────────────────────────────────────────

    getIntegrations(): Observable<EntityIntegration[]> {
        return this.http.get<EntityIntegration[]>(this.baseUrl);
    }

    upsertIntegration(type: string, request: UpsertIntegrationRequest): Observable<EntityIntegration> {
        return this.http.put<EntityIntegration>(`${this.baseUrl}/${type}`, request);
    }

    deleteIntegration(type: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/${type}`);
    }

    // ── Unified Financial Connections ───────────────────────────────────────

    /**
     * Starts the bank connection flow.
     * Returns type="none" for Manual/CSV — no external step required.
     */
    initiateConnection(): Observable<ConnectionInitResult> {
        return this.http.post<ConnectionInitResult>(`${this.connectionsUrl}/initiate`, {});
    }

    /**
     * Completes the connection flow.
     * `payload` is the public_token for Plaid, empty for Basiq/Manual, or JSON for CSV.
     */
    completeConnection(payload = ''): Observable<FinancialConnection> {
        return this.http.post<FinancialConnection>(`${this.connectionsUrl}/complete`, { payload });
    }

    /** Returns all connections across all providers. */
    getConnections(): Observable<FinancialConnection[]> {
        return this.http.get<FinancialConnection[]>(this.connectionsUrl);
    }

    /** Triggers an immediate sync for a connection. */
    syncConnection(connectionId: string, startDate?: Date): Observable<SyncResult> {
        return this.http.post<SyncResult>(
            `${this.connectionsUrl}/${connectionId}/sync`,
            startDate ? { startDate: startDate.toISOString() } : {}
        );
    }

    /** Removes a connection and revokes credentials at the provider. */
    removeConnection(connectionId: string): Observable<void> {
        return this.http.delete<void>(`${this.connectionsUrl}/${connectionId}`);
    }

    /** Returns all connected accounts for the entity. */
    getConnectedAccounts(): Observable<ConnectedAccount[]> {
        return this.http.get<ConnectedAccount[]>(`${this.connectionsUrl}/accounts`);
    }

    /** Links a ConnectedAccount to a system Account for balance sync. */
    linkAccount(connectedAccountId: string, linkedAccountId: string): Observable<void> {
        return this.http.put<void>(
            `${this.connectionsUrl}/accounts/${connectedAccountId}/link`,
            { linkedAccountId }
        );
    }

    /** Returns the health status of every connection. */
    getConnectionHealth(): Observable<ConnectionHealth[]> {
        return this.http.get<ConnectionHealth[]>(`${this.connectionsUrl}/health`);
    }
}
