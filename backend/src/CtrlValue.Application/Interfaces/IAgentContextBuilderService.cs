using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentContextBuilderService
{
    /// <summary>
    /// Builds a complete FinanceContextDto for the entity.
    /// Checks AgentContextSnapshot cache first (30-min TTL).
    /// Pass forceRefresh=true to bypass the cache.
    /// </summary>
    Task<FinanceContextDto> BuildContextAsync(
        Guid userId,
        Guid entityId,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
