using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Fetches and caches macro economic summaries via the configured LLM provider.
/// Topics are predefined — the service does not accept arbitrary search queries.
/// entityId is used to resolve the per-entity API key via the 3-tier key hierarchy.
/// </summary>
public interface IAgentWebResearchService
{
    /// <summary>
    /// Returns the macro summary for the given topic key.
    /// Fetches fresh via the configured provider (with web search if supported) if the cached entry is expired.
    /// </summary>
    Task<MacroSummaryDto> GetMacroSummaryAsync(string topicKey, Guid entityId, CancellationToken ct = default);

    /// <summary>Returns summaries for all supported macro topics (cached or fresh as needed).</summary>
    Task<List<MacroSummaryDto>> GetAllMacroSummariesAsync(Guid entityId, CancellationToken ct = default);
}
