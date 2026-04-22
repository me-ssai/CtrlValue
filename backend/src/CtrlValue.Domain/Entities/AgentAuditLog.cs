namespace CtrlValue.Domain.Entities;

/// <summary>
/// Immutable record of every LLM request made by the agent system.
/// Does NOT extend BaseEntity — no soft-delete, no TenantId, no UpdatedAt.
/// Append-only by convention. Never update or delete rows.
/// </summary>
public class AgentAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ConversationId { get; set; }
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    /// <summary>"chat", "insight", "macro", "context"</summary>
    public string RequestType { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptTemplateVersion { get; set; } = string.Empty;
    /// <summary>Jsonb: the assembled prompt context sent to the model.</summary>
    public string? InputPayload { get; set; }
    /// <summary>Jsonb: the raw model response.</summary>
    public string? OutputPayload { get; set; }
    /// <summary>Jsonb: array of tool names invoked (e.g. ["web_search_preview"]).</summary>
    public string? ToolsUsed { get; set; }
    /// <summary>Jsonb: array of source labels used.</summary>
    public string? SourcesUsed { get; set; }
    /// <summary>"pass", "blocked", or "modified"</summary>
    public string? SafetyDecision { get; set; }
    public int? TotalTokens { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
