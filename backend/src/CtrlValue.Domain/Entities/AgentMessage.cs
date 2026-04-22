using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class AgentMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public AgentMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>Jsonb: structured tool call results or structured response payload.</summary>
    public string? StructuredPayload { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    /// <summary>Jsonb: array of tool call descriptors used in this turn.</summary>
    public string? ToolCalls { get; set; }
    /// <summary>Source attribution: "internal", "web", or "hybrid". Drives badge in chat UI.</summary>
    public string? SourceType { get; set; }

    public AgentConversation Conversation { get; set; } = null!;
}
