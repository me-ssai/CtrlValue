using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

/// <summary>
/// Provider-agnostic representation of a bank/institution connection.
/// Replaces the old PlaidConnection and BasiqConnection entities.
/// One row per institution per entity — each entity can have multiple connections.
/// EncryptedCredential is AES-256 encrypted and must never be logged or returned via API.
/// </summary>
public class FinancialConnection : BaseEntity
{
    public Guid EntityId { get; set; }

    public FinancialConnectionProvider Provider { get; set; }

    /// <summary>
    /// Provider-specific opaque identifier:
    ///   Plaid  → ItemId
    ///   Basiq  → BasiqConnectionId
    ///   Csv    → synthetic ID (GUID string)
    ///   Manual → synthetic ID (GUID string)
    /// </summary>
    public string ProviderConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// AES-256 encrypted access credential:
    ///   Plaid  → access_token
    ///   Basiq  → BasiqUserId (shared across all Basiq connections for the same entity)
    ///   Csv    → empty
    ///   Manual → empty
    /// </summary>
    public string EncryptedCredential { get; set; } = string.Empty;

    public string InstitutionId { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>Base64-encoded institution logo or URL. Optional.</summary>
    public string? InstitutionLogoUrl { get; set; }

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;

    /// <summary>Human-readable error detail when Status is Error or NeedsReauth.</summary>
    public string? StatusMessage { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code. Drives provider selection.</summary>
    public string Country { get; set; } = "AU";

    public DateTime? ConsentExpiresAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? LastSyncAttemptedAt { get; set; }

    // Navigation
    public Entity Entity { get; set; } = null!;
    public ICollection<ConnectedAccount> ConnectedAccounts { get; set; } = new List<ConnectedAccount>();
    public ICollection<ConnectionSyncLog> SyncLogs { get; set; } = new List<ConnectionSyncLog>();
}
