namespace CtrlValue.Domain.Entities;

/// <summary>
/// Key-value store for global agent configuration (e.g. DefaultProvider).
/// TenantId is left as empty string — these are system-global records.
/// </summary>
public class AgentSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
