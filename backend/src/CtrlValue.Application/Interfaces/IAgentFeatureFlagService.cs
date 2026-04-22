using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

public interface IAgentFeatureFlagService
{
    /// <summary>Returns all feature flags with their global-level enabled state.</summary>
    Task<List<AgentFeatureFlagDto>> GetAllFlagsAsync();

    /// <summary>
    /// Returns the effective enabled state for a section for a given user.
    /// Resolution order: user-level assignment → global flag.
    /// Note: does NOT check AgentCore here — use IsSectionAccessibleAsync for full gate.
    /// </summary>
    Task<bool> IsFlagEnabledForUserAsync(Guid userId, AgentSectionKey section);

    /// <summary>
    /// Full gate check: AgentCore must be on AND the specified section must be on for this user.
    /// Use this in every controller/service endpoint.
    /// </summary>
    Task<bool> IsSectionAccessibleAsync(Guid userId, AgentSectionKey section);

    /// <summary>Returns the effective config for a user (used by GET /api/agent/config).</summary>
    Task<AgentConfigDto> GetAgentConfigForUserAsync(Guid userId);

    Task<AgentFeatureFlagDto> UpdateGlobalFlagAsync(AgentSectionKey section, bool isEnabled);

    Task SetUserOverrideAsync(Guid userId, AgentSectionKey section, bool isEnabled);

    Task RemoveUserOverrideAsync(Guid userId, AgentSectionKey section);

    Task<List<UserFeatureFlagOverrideDto>> GetUserOverridesAsync(Guid userId);
}
