using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentInsightService
{
    /// <summary>Returns all non-dismissed insights for the entity.</summary>
    Task<List<AgentInsightDto>> GetInsightsAsync(Guid userId, Guid entityId);

    /// <summary>
    /// Runs the full rule-based insight detection pipeline and upserts results.
    /// Existing undismissed insights of the same type are replaced.
    /// </summary>
    Task RefreshInsightsAsync(Guid userId, Guid entityId, CancellationToken ct = default);

    /// <summary>Soft-dismisses an insight (sets IsDismissed=true, DismissedAt=now).</summary>
    Task DismissInsightAsync(Guid insightId, Guid userId);
}
