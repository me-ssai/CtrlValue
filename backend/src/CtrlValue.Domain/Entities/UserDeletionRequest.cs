namespace CtrlValue.Domain.Entities;

public class UserDeletionRequest : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Pending | Cancelled | ExpediteRequested | Completed
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Scheduled hard-deletion date — defaults to 30 days after the request was created.
    /// </summary>
    public DateTime ScheduledDeletionAt { get; set; }

    public DateTime? ExpediteRequestedAt { get; set; }

    /// <summary>
    /// The user (admin or delegated approver) who reviewed an expedited-deletion request.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
