namespace CtrlValue.Domain.Entities;

public class EntityRolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;

    // Navigation
    public EntityCustomRole Role { get; set; } = null!;
}
