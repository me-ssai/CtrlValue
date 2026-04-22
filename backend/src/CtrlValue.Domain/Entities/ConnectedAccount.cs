namespace CtrlValue.Domain.Entities;

/// <summary>
/// Provider-agnostic representation of a single account returned by any IFinancialProvider.
/// Replaces the old PlaidAccount and BasiqAccount entities.
/// Can optionally be linked to an existing Account in the system via LinkedAccountId,
/// which enables balance sync and transaction import for that account.
/// </summary>
public class ConnectedAccount : BaseEntity
{
    public Guid ConnectionId { get; set; }

    /// <summary>Denormalised for efficient per-entity queries without joining through FinancialConnection.</summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Provider's stable identifier for this account:
    ///   Plaid  → account_id
    ///   Basiq  → account id
    ///   Csv    → synthetic ID (GUID string)
    ///   Manual → synthetic ID (GUID string)
    /// </summary>
    public string ExternalAccountId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? OfficialName { get; set; }

    /// <summary>Last 4 digits of account number, BSB mask, etc. Provider-dependent.</summary>
    public string? Mask { get; set; }

    /// <summary>Account type: depository | credit | loan | investment | transaction | savings | etc.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Account subtype: checking | savings | credit card | mortgage | etc.</summary>
    public string? Subtype { get; set; }

    public decimal? CurrentBalance { get; set; }
    public decimal? AvailableBalance { get; set; }
    public string CurrencyCode { get; set; } = "AUD";
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// FK to the Account in the system this ConnectedAccount is mapped to.
    /// Null until the user links them. Once linked, balance syncs and transaction
    /// imports target this Account.
    /// </summary>
    public Guid? LinkedAccountId { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    // Navigation
    public FinancialConnection Connection { get; set; } = null!;
    public Entity Entity { get; set; } = null!;
    public Account? LinkedAccount { get; set; }
}
