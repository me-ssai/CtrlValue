namespace CtrlValue.Domain.Entities;

public class EntityUser : BaseEntity
{
    public Guid EntityId { get; set; }
    public Guid UserId { get; set; }
    public Guid CustomRoleId { get; set; }

    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public User User { get; set; } = null!;
    public EntityCustomRole CustomRole { get; set; } = null!;
}
