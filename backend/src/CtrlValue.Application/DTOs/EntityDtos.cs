namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Entity DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class EntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "AUD";
    public string Country { get; set; } = "AU";
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<EntityUserDto> Users { get; set; } = new();
}

public class CreateEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "AUD";
    public string Country { get; set; } = "AU";
}

public class UpdateEntityRequest
{
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "AUD";
    public string Country { get; set; } = "AU";
}

// ═══════════════════════════════════════════════════════════════════════════
// EntityUser DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class EntityUserDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Guid CustomRoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddEntityUserRequest
{
    public string UserEmail { get; set; } = string.Empty;
    public Guid CustomRoleId { get; set; }
}

public class UpdateEntityUserRequest
{
    public Guid CustomRoleId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// EntityCustomRole DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class EntityCustomRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class CreateEntityCustomRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

public class UpdateEntityCustomRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}
