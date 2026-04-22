namespace CtrlValue.Domain.Entities;

/// <summary>
/// Immutable audit trail record. Does NOT extend BaseEntity intentionally —
/// audit logs are never soft-deleted and have no UpdatedAt semantics.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant context — "system" for events with no authenticated tenant.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>User who performed the action. Guid.Empty for anonymous/system events.</summary>
    public Guid UserId { get; set; }

    /// <summary>Workspace (entity) context, if applicable.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Dot-separated action name, e.g. "user.login", "user.login.failed",
    /// "account.created", "import.file.uploaded", "admin.impersonation".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Domain object type involved, e.g. "Account", "Transaction".</summary>
    public string? ObjectType { get; set; }

    /// <summary>ID of the domain object involved.</summary>
    public string? ObjectId { get; set; }

    /// <summary>JSON payload with additional context. Passwords/tokens must never appear here.</summary>
    public string? Detail { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
