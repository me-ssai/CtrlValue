using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

/// <summary>
/// Platform-level feature flag for a single Agent section.
/// One row per AgentSectionKey, seeded by migration.
/// Individual user overrides are stored in AgentFeatureFlagAssignment.
/// TenantId is left as empty string — these are system-global records.
/// </summary>
public class AgentFeatureFlag : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = false;
    public AgentSectionKey SectionKey { get; set; }

    public ICollection<AgentFeatureFlagAssignment> Assignments { get; set; } = new List<AgentFeatureFlagAssignment>();
}
