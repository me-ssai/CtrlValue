using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAgentScenarioService
{
    /// <summary>
    /// Runs a deterministic what-if scenario against the user's current financial context.
    /// No LLM call is made — all calculations are rule-based. Persists the result.
    /// </summary>
    Task<ScenarioResultDto> RunScenarioAsync(
        Guid userId,
        Guid entityId,
        RunScenarioRequest request,
        CancellationToken ct = default);

    /// <summary>Returns persisted scenario runs for an entity, newest first.</summary>
    Task<List<AgentScenarioHistoryDto>> GetScenarioHistoryAsync(
        Guid userId,
        Guid entityId,
        int limit = 20);
}
