namespace CtrlValue.Application.DTOs;

public record AuditLogDto(
    Guid Id,
    string TenantId,
    Guid UserId,
    string? UserEmail,
    Guid? EntityId,
    string? EntityName,
    string Action,
    string? ObjectType,
    string? ObjectId,
    string? Detail,
    string? IpAddress,
    DateTime Timestamp
);

public class AuditLogQueryParams
{
    public string? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? EntityId { get; set; }

    /// <summary>Prefix filter, e.g. "user." returns all user.* events.</summary>
    public string? Action { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
