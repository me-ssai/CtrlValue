namespace CtrlValue.Domain.Entities;

public class EntityCustomRole : BaseEntity
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// System roles (Owner, Editor, Viewer) are created automatically per entity
    /// and cannot be deleted. Custom roles are tenant-created.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    // Navigation
    public Entity Entity { get; set; } = null!;
    public ICollection<EntityRolePermission> RolePermissions { get; set; } = new List<EntityRolePermission>();
    public ICollection<EntityUser> EntityUsers { get; set; } = new List<EntityUser>();
}
