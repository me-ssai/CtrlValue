import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AdminService, AdminUser, TenantRecord } from '../../services/admin.service';
import { AgentService, AgentFeatureFlagDto, UserFeatureFlagOverrideDto, AgentAuditLogDto, AgentDigestEmailDto } from '../intelligence/agent/agent.service';
import { environment } from '@env/environment';

interface PlatformIntegration {
    id: string;
    integrationType: string;
    isEnabled: boolean;
    hasApiKey: boolean;
    lastUsedAt: string | null;
    createdAt: string;
}

interface PlatformIntegrationForm {
    apiKey: string;
    isEnabled: boolean;
    showKey: boolean;
}

@Component({
    selector: 'app-super-admin',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './super-admin.component.html',
    styleUrls: ['./super-admin.component.css']
})
export class SuperAdminComponent implements OnInit {
    private adminService = inject(AdminService);
    private http = inject(HttpClient);
    private agentService = inject(AgentService);
    private sanitizer = inject(DomSanitizer);

    activeTab: 'users' | 'tenants' | 'integrations' | 'agent' = 'users';

    // ── Agent feature flags ──
    agentFlags: AgentFeatureFlagDto[] = [];
    agentFlagsLoading = false;
    agentUserSearchId = '';
    agentUserOverrides: UserFeatureFlagOverrideDto[] = [];
    agentAuditLogs: AgentAuditLogDto[] = [];
    agentAuditLoading = false;

    // ── Agent settings ──
    readonly providerOptions = ['OpenAI', 'Anthropic'];
    agentSelectedProvider = 'OpenAI';
    agentSettingsSaving = false;

    // ── Agent digest approval ──
    pendingDigests: AgentDigestEmailDto[] = [];
    pendingDigestsLoading = false;
    previewDigest: AgentDigestEmailDto | null = null;

    // Platform integrations state
    readonly integrationTypes = ['ALPHA_VANTAGE', 'METALS_API', 'COINGECKO', 'OPENAI', 'ANTHROPIC'];
    readonly integrationLabels: Record<string, string> = {
        ALPHA_VANTAGE: 'Alpha Vantage',
        METALS_API:    'Metals API',
        COINGECKO:     'CoinGecko',
        OPENAI:        'OpenAI',
        ANTHROPIC:     'Anthropic',
    };
    readonly integrationDescriptions: Record<string, string> = {
        ALPHA_VANTAGE: 'Used for stock & ETF price fetching. Free tier: 25 calls/day.',
        METALS_API:    'Used for precious metals (Gold, Silver, Platinum). Free tier: 50 calls/month.',
        COINGECKO:     'Used for cryptocurrency prices. Public API — no key required for basic usage.',
        OPENAI:        'Platform-level OpenAI API key for CtrlValue Agent chat and macro insights.',
        ANTHROPIC:     'Platform-level Anthropic API key for CtrlValue Agent using Claude models.',
    };
    platformIntegrations: Record<string, PlatformIntegration> = {};
    integrationForms: Record<string, PlatformIntegrationForm> = {
        ALPHA_VANTAGE: { apiKey: '', isEnabled: false, showKey: false },
        METALS_API:    { apiKey: '', isEnabled: false, showKey: false },
        COINGECKO:     { apiKey: '', isEnabled: false, showKey: false },
        OPENAI:        { apiKey: '', isEnabled: true, showKey: false },
        ANTHROPIC:     { apiKey: '', isEnabled: true, showKey: false },
    };
    users: AdminUser[] = [];
    tenants: TenantRecord[] = [];
    userSearch = '';
    newTenantName = '';
    newTenantEmail = '';
    statusMessage = '';
    isError = false;
    priceFetchRunning = false;

    safeHtml(html: string): SafeHtml {
        return this.sanitizer.bypassSecurityTrustHtml(html);
    }

    ngOnInit() {
        this.loadUsers();
        this.loadTenants();
        this.loadPlatformIntegrations();
    }

    // ── Price Fetch Trigger ────────────────────────────────────────────────

    triggerPriceFetch() {
        this.priceFetchRunning = true;
        this.http.post(`${environment.apiUrl}/super-admin/price-fetch/trigger`, {}).subscribe({
            next: () => {
                this.priceFetchRunning = false;
                this.showStatus('Price fetch completed successfully.');
            },
            error: () => {
                this.priceFetchRunning = false;
                this.showStatus('Price fetch failed — check server logs.', true);
            }
        });
    }

    // ── Platform Integrations ──────────────────────────────────────────────

    loadPlatformIntegrations() {
        this.http.get<PlatformIntegration[]>(`${environment.apiUrl}/platform-integrations`).subscribe({
            next: rows => {
                for (const row of rows) {
                    this.platformIntegrations[row.integrationType] = row;
                    this.integrationForms[row.integrationType].isEnabled = row.isEnabled;
                }
            },
            error: () => this.showStatus('Failed to load platform integrations', true)
        });
    }

    savePlatformKey(type: string) {
        const form = this.integrationForms[type];
        const payload = {
            apiKey: form.apiKey || null,
            isEnabled: form.isEnabled
        };
        this.http.put<PlatformIntegration>(`${environment.apiUrl}/platform-integrations/${type}`, payload).subscribe({
            next: row => {
                this.platformIntegrations[type] = row;
                this.integrationForms[type].apiKey = ''; // clear after save
                this.showStatus(`${this.integrationLabels[type]} key saved`);
            },
            error: () => this.showStatus('Failed to save integration key', true)
        });
    }

    deletePlatformKey(type: string) {
        if (!confirm(`Remove the ${this.integrationLabels[type]} platform key?`)) return;
        this.http.delete(`${environment.apiUrl}/platform-integrations/${type}`).subscribe({
            next: () => {
                delete this.platformIntegrations[type];
                this.integrationForms[type] = { apiKey: '', isEnabled: false, showKey: false };
                this.showStatus(`${this.integrationLabels[type]} key removed`);
            },
            error: () => this.showStatus('Failed to remove integration key', true)
        });
    }

    get filteredUsers(): AdminUser[] {
        const q = this.userSearch.toLowerCase();
        return this.users.filter(u =>
            !q || u.email.toLowerCase().includes(q) ||
            `${u.firstName} ${u.lastName}`.toLowerCase().includes(q) ||
            u.tenantId.toLowerCase().includes(q)
        );
    }

    loadUsers() {
        this.adminService.getAllUsers().subscribe({
            next: users => this.users = users,
            error: () => this.showStatus('Failed to load users', true)
        });
    }

    loadTenants() {
        this.adminService.getAllTenants().subscribe({
            next: tenants => this.tenants = tenants,
            error: () => this.showStatus('Failed to load tenants', true)
        });
    }

    updateRole(user: AdminUser) {
        this.adminService.updateUserRole(user.id, user.role).subscribe({
            next: () => this.showStatus(`Role updated for ${user.email}`),
            error: () => this.showStatus('Failed to update role', true)
        });
    }

    resetPassword(user: AdminUser) {
        this.adminService.resetPasswordForUser(user.id).subscribe({
            next: () => this.showStatus(`Password reset email sent to ${user.email}`),
            error: () => this.showStatus('Failed to send reset email', true)
        });
    }

    deleteUser(user: AdminUser) {
        if (!confirm(`Permanently delete ${user.email}?\n\nThis will remove all their workspaces, accounts, transactions, and investments. This action cannot be undone.`)) return;
        this.adminService.deleteUser(user.id).subscribe({
            next: () => {
                this.users = this.users.filter(u => u.id !== user.id);
                this.showStatus(`${user.email} has been permanently deleted`);
            },
            error: () => this.showStatus('Failed to delete user', true)
        });
    }

    impersonate(user: AdminUser) {
        if (!confirm(`Impersonate ${user.email}? This action is logged.`)) return;
        this.adminService.impersonateUser(user.id).subscribe({
            next: () => {
                // Backend sets the impersonation token as an httpOnly cookie.
                window.location.href = '/dashboard';
            },
            error: () => this.showStatus('Impersonation failed', true)
        });
    }

    createTenant() {
        if (!this.newTenantName || !this.newTenantEmail) return;
        this.adminService.createTenant(this.newTenantName, this.newTenantEmail).subscribe({
            next: t => {
                this.tenants.unshift(t);
                this.newTenantName = '';
                this.newTenantEmail = '';
                this.showStatus(`Tenant "${t.name}" created`);
            },
            error: () => this.showStatus('Failed to create tenant', true)
        });
    }

    deleteTenant(tenant: TenantRecord) {
        if (!confirm(`Permanently delete tenant "${tenant.name}"?\n\nThis will remove all users, workspaces, accounts, transactions, and investments belonging to this tenant. This action cannot be undone.`)) return;
        this.adminService.deleteTenant(tenant.id).subscribe({
            next: () => {
                this.tenants = this.tenants.filter(t => t.id !== tenant.id);
                this.showStatus(`Tenant "${tenant.name}" has been permanently deleted`);
            },
            error: () => this.showStatus('Failed to delete tenant', true)
        });
    }

    toggleTenant(tenant: TenantRecord) {
        this.adminService.setTenantActive(tenant.id, !tenant.isActive).subscribe({
            next: () => { tenant.isActive = !tenant.isActive; this.showStatus('Tenant status updated'); },
            error: () => this.showStatus('Failed to update tenant', true)
        });
    }

    private showStatus(msg: string, error = false) {
        this.statusMessage = msg;
        this.isError = error;
        setTimeout(() => this.statusMessage = '', 3500);
    }

    // ── Agent Feature Flags ──────────────────────────────────────────────────

    loadAgentFlags() {
        this.agentFlagsLoading = true;
        this.agentService.adminGetFlags().subscribe({
            next: (flags) => { this.agentFlags = flags; this.agentFlagsLoading = false; },
            error: () => { this.agentFlagsLoading = false; this.showStatus('Failed to load agent flags', true); }
        });
    }

    toggleAgentFlag(flag: AgentFeatureFlagDto) {
        this.agentService.adminUpdateFlag(flag.sectionKey, !flag.isEnabled).subscribe({
            next: (updated) => {
                const idx = this.agentFlags.findIndex(f => f.id === updated.id);
                if (idx >= 0) this.agentFlags[idx] = updated;
                this.showStatus(`${updated.name} ${updated.isEnabled ? 'enabled' : 'disabled'}`);
            },
            error: () => this.showStatus('Failed to update flag', true)
        });
    }

    loadUserOverrides() {
        if (!this.agentUserSearchId.trim()) return;
        this.agentService.adminGetUserOverrides(this.agentUserSearchId.trim()).subscribe({
            next: (overrides) => { this.agentUserOverrides = overrides; },
            error: () => this.showStatus('Failed to load user overrides', true)
        });
    }

    toggleUserOverride(override: UserFeatureFlagOverrideDto) {
        this.agentService.adminSetUserOverride(
            this.agentUserSearchId.trim(),
            override.flagKey,
            !override.isEnabled
        ).subscribe({
            next: () => {
                override.isEnabled = !override.isEnabled;
                this.showStatus('Override updated');
            },
            error: () => this.showStatus('Failed to update override', true)
        });
    }

    loadAgentAuditLogs() {
        this.agentAuditLoading = true;
        this.agentService.adminGetAuditLogs().subscribe({
            next: (logs) => { this.agentAuditLogs = logs; this.agentAuditLoading = false; },
            error: () => { this.agentAuditLoading = false; this.showStatus('Failed to load audit logs', true); }
        });
    }

    onAgentTabSelect() {
        if (this.agentFlags.length === 0) {
            this.loadAgentFlags();
            this.loadAgentSettings();
            this.loadPendingDigests();
        }
    }

    loadPendingDigests() {
        this.pendingDigestsLoading = true;
        this.agentService.adminGetPendingDigests().subscribe({
            next: (digests) => { this.pendingDigests = digests; this.pendingDigestsLoading = false; },
            error: () => { this.pendingDigestsLoading = false; this.showStatus('Failed to load pending digests', true); }
        });
    }

    approveDigest(digest: AgentDigestEmailDto) {
        this.agentService.adminApproveDigest(digest.id).subscribe({
            next: () => {
                this.pendingDigests = this.pendingDigests.filter(d => d.id !== digest.id);
                this.previewDigest = null;
                this.showStatus(`Digest "${digest.weekKey}" approved`);
            },
            error: () => this.showStatus('Failed to approve digest', true)
        });
    }

    rejectDigest(digest: AgentDigestEmailDto) {
        this.agentService.adminRejectDigest(digest.id).subscribe({
            next: () => {
                this.pendingDigests = this.pendingDigests.filter(d => d.id !== digest.id);
                this.previewDigest = null;
                this.showStatus(`Digest "${digest.weekKey}" rejected`);
            },
            error: () => this.showStatus('Failed to reject digest', true)
        });
    }

    loadAgentSettings() {
        this.http.get<Record<string, string>>(`${environment.apiUrl}/admin/agent/settings`).subscribe({
            next: (settings) => {
                if (settings['DefaultProvider']) {
                    this.agentSelectedProvider = settings['DefaultProvider'];
                }
            },
            error: () => this.showStatus('Failed to load agent settings', true)
        });
    }

    saveAgentProvider() {
        this.agentSettingsSaving = true;
        this.http.put(`${environment.apiUrl}/admin/agent/settings/DefaultProvider`,
            { value: this.agentSelectedProvider }
        ).subscribe({
            next: () => {
                this.agentSettingsSaving = false;
                this.showStatus(`Default provider set to ${this.agentSelectedProvider}`);
            },
            error: () => {
                this.agentSettingsSaving = false;
                this.showStatus('Failed to save provider setting', true);
            }
        });
    }
}
