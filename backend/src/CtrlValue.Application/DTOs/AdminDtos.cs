namespace CtrlValue.Application.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<EntityMembershipDto> Entities { get; set; } = new();
}

public class EntityMembershipDto
{
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
}

public class InviteUserRequest
{
    public string Email { get; set; } = string.Empty;
}

public class UpdateUserRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class UserDeletionRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime ScheduledDeletionAt { get; set; }
    public DateTime? ExpediteRequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class RejectDeletionRequest
{
    public string? Reason { get; set; }
}

public class SetDeletionApproverRequest
{
    public bool CanApprove { get; set; }
}
