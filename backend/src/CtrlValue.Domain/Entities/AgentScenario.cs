namespace CtrlValue.Domain.Entities;

/// <summary>Persisted record of a what-if scenario run.</summary>
public class AgentScenario : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public string ScenarioType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RequestPayload { get; set; } = string.Empty;   // JSON
    public string ResultPayload { get; set; } = string.Empty;    // JSON
    public string Currency { get; set; } = "AUD";
}
