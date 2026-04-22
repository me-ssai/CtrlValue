namespace CtrlValue.Domain.Entities;

/// <summary>
/// A generated weekly financial digest email awaiting or having received admin approval.
/// </summary>
public class AgentDigestEmail : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected, Sent</summary>
    public string Status { get; set; } = "Pending";

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>ISO week key, e.g. "2026-W14"</summary>
    public string WeekKey { get; set; } = string.Empty;
}
