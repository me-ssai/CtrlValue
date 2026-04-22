using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentOrchestratorService
{
    /// <summary>
    /// Handles a user chat message end-to-end:
    /// checks flags → builds context → assembles prompt → calls provider →
    /// persists messages → writes audit log → returns response.
    /// </summary>
    Task<AgentChatResponse> ChatAsync(
        Guid userId,
        Guid entityId,
        SendMessageRequest request,
        CancellationToken ct = default);

    /// <summary>Returns paginated conversation list for the user/entity.</summary>
    Task<List<AgentConversationDto>> GetConversationsAsync(Guid userId, Guid entityId);

    /// <summary>Returns messages for a specific conversation (owned by userId).</summary>
    Task<List<AgentMessageDto>> GetMessagesAsync(Guid conversationId, Guid userId);
}
