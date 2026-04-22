using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class AgentConversation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AgentProviderName Provider { get; set; } = AgentProviderName.OpenAI;
    public string ModelName { get; set; } = string.Empty;
    public AgentConversationSectionType SectionType { get; set; } = AgentConversationSectionType.Chat;

    public User User { get; set; } = null!;
    public Entity Entity { get; set; } = null!;
    public ICollection<AgentMessage> Messages { get; set; } = new List<AgentMessage>();
}
