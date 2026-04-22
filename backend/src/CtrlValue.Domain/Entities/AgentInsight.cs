using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class AgentInsight : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public AgentInsightType InsightType { get; set; }
    public AgentInsightSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    /// <summary>Jsonb: array of { label, value, unit } evidence objects.</summary>
    public string? Evidence { get; set; }
    public AgentInsightSourceType SourceType { get; set; } = AgentInsightSourceType.Internal;
    public bool IsDismissed { get; set; } = false;
    public DateTime? DismissedAt { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
