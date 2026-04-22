namespace CtrlValue.Domain.Entities;

/// <summary>
/// Cached finance context snapshot per entity.
/// Payload is the full FinanceContextDto serialised as JSON.
/// Rebuilt when ExpiresAt is past or forceRefresh is requested.
/// Does not participate in soft-delete — cache rows are replaced, not logically deleted.
/// </summary>
public class AgentContextSnapshot : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    /// <summary>"full" for the default full-context snapshot.</summary>
    public string SnapshotType { get; set; } = "full";
    public DateTime AsOfDate { get; set; }
    /// <summary>Full FinanceContextDto serialised as JSON.</summary>
    public string Payload { get; set; } = string.Empty;
    /// <summary>SHA256 of key input fields — allows change detection.</summary>
    public string? Hash { get; set; }
    public DateTime ExpiresAt { get; set; }
}
