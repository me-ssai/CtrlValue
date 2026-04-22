namespace CtrlValue.Domain.Entities;

/// <summary>
/// Immutable audit log for every sync attempt against a FinancialConnection.
/// Created after each sync (success or failure) by the ConnectionService.
/// </summary>
public class ConnectionSyncLog : BaseEntity
{
    public Guid ConnectionId { get; set; }

    /// <summary>Success | Failed | PartialSuccess</summary>
    public string Status { get; set; } = string.Empty;

    public int? AccountsSynced { get; set; }
    public int? TransactionsStaged { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan? Duration { get; set; }

    // Navigation
    public FinancialConnection Connection { get; set; } = null!;
}
