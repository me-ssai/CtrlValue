namespace CtrlValue.Domain.Entities;

/// <summary>
/// Per-user override for a global agent feature flag.
/// Absence of a row = use the global flag value.
/// IsEnabled here takes precedence over the global flag.
/// </summary>
public class AgentFeatureFlagAssignment : BaseEntity
{
    public Guid FeatureFlagId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? EntityId { get; set; }
    public bool IsEnabled { get; set; }

    public AgentFeatureFlag FeatureFlag { get; set; } = null!;
    public User? User { get; set; }
    public Entity? Entity { get; set; }
}
