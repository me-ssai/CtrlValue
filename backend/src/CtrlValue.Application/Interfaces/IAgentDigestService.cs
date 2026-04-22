using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentDigestService
{
    /// <summary>Generates a weekly digest for the entity and saves it as Pending.</summary>
    Task<AgentDigestEmailDto> GenerateDigestAsync(
        Guid userId,
        Guid entityId,
        CancellationToken ct = default);

    /// <summary>Returns all pending digests (admin view).</summary>
    Task<List<AgentDigestEmailDto>> GetPendingDigestsAsync();

    /// <summary>Returns all digests (admin view), paged.</summary>
    Task<List<AgentDigestEmailDto>> GetAllDigestsAsync(int page = 1, int pageSize = 50);

    /// <summary>Approves a digest for sending.</summary>
    Task ApproveDigestAsync(Guid digestId, Guid approverUserId);

    /// <summary>Rejects a digest.</summary>
    Task RejectDigestAsync(Guid digestId, Guid approverUserId);

    /// <summary>Marks approved digests as sent (called by the email-send step).</summary>
    Task MarkSentAsync(Guid digestId);
}
