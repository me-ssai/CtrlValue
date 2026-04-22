using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Core abstraction over any financial data provider (Plaid, Basiq, CSV, Manual).
/// All provider implementations must be scoped services registered with DI.
/// The ConnectionService resolves the correct implementation via IFinancialProviderFactory.
/// </summary>
public interface IFinancialProvider
{
    FinancialConnectionProvider ProviderType { get; }

    /// <summary>
    /// Initiates the connection flow.
    /// Returns a ConnectionInitResult whose Type determines how the frontend proceeds:
    ///   "link_token" → open Plaid Link widget
    ///   "auth_url"   → open Basiq consent URL in popup
    ///   "none"       → no external flow required (Manual/CSV)
    /// </summary>
    Task<ConnectionInitResult> InitiateConnectionAsync(Guid entityId, Guid userId, string tenantId, string? mobile = null);

    /// <summary>
    /// Completes the connection flow after the user returns from the external provider.
    ///   Plaid  → callbackPayload is the public_token from Link onSuccess
    ///   Basiq  → callbackPayload is empty (we poll Basiq for new connections)
    ///   CSV    → callbackPayload is a JSON blob describing the pending file import
    ///   Manual → callbackPayload is empty
    /// Creates and persists a FinancialConnection; performs initial account sync.
    /// </summary>
    Task<FinancialConnectionDto> CompleteConnectionAsync(Guid entityId, string tenantId, string callbackPayload);

    /// <summary>Fetches the latest account balances and updates ConnectedAccount rows.</summary>
    Task SyncAccountsAsync(FinancialConnection connection);

    /// <summary>
    /// Fetches transactions for the given date range and upserts them into the staging table.
    /// Deduplicates by the provider's transaction ID.
    /// Returns the number of newly staged rows.
    /// </summary>
    Task<int> SyncTransactionsAsync(FinancialConnection connection, DateTime startDate, DateTime endDate);

    /// <summary>Revokes credentials at the provider and performs any provider-side cleanup.</summary>
    Task RevokeConnectionAsync(FinancialConnection connection);

    /// <summary>Returns true if this provider supports the given ISO-3166-1 alpha-2 country code.</summary>
    bool SupportsCountry(string countryCode);
}

/// <summary>Result of InitiateConnectionAsync.</summary>
public record ConnectionInitResult(
    /// <summary>"link_token" | "auth_url" | "none"</summary>
    string Type,
    /// <summary>The actual link token or URL value. Empty when Type is "none".</summary>
    string Value
);

/// <summary>Lightweight DTO returned after completing or fetching a connection.</summary>
public record FinancialConnectionDto(
    Guid Id,
    Guid EntityId,
    string Provider,
    string ProviderConnectionId,
    string InstitutionName,
    string? InstitutionLogoUrl,
    string Status,
    string? StatusMessage,
    string Country,
    DateTime? LastSyncedAt,
    DateTime? ConsentExpiresAt,
    int AccountCount
);

/// <summary>DTO for a single connected account (provider-agnostic).</summary>
public record ConnectedAccountDto(
    Guid Id,
    Guid ConnectionId,
    string ExternalAccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string Type,
    string? Subtype,
    decimal? CurrentBalance,
    decimal? AvailableBalance,
    string CurrencyCode,
    bool IsActive,
    Guid? LinkedAccountId,
    DateTime? LastSyncedAt
);

/// <summary>Result of a sync operation.</summary>
public record ConnectionSyncResultDto(
    bool AccountsSynced,
    int TransactionsStaged,
    string Status,
    string? ErrorMessage
);

/// <summary>Health summary for a single connection.</summary>
public record ConnectionHealthDto(
    Guid ConnectionId,
    string InstitutionName,
    string Provider,
    /// <summary>"Healthy" | "NeedsReauth" | "Error" | "Expired"</summary>
    string HealthStatus,
    DateTime? LastSyncedAt,
    DateTime? ConsentExpiresAt,
    string? StatusMessage
);
