using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentFeatureFlagService : IAgentFeatureFlagService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IAgentSettingService _settings;
    private readonly ILogger<AgentFeatureFlagService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public AgentFeatureFlagService(
        AppDbContext db,
        IMemoryCache cache,
        IAgentSettingService settings,
        ILogger<AgentFeatureFlagService> logger)
    {
        _db = db;
        _cache = cache;
        _settings = settings;
        _logger = logger;
    }

    public async Task<List<AgentFeatureFlagDto>> GetAllFlagsAsync()
    {
        var flags = await _db.AgentFeatureFlags
            .AsNoTracking()
            .OrderBy(f => f.SectionKey)
            .ToListAsync();

        return flags.Select(Map).ToList();
    }

    public async Task<bool> IsFlagEnabledForUserAsync(Guid userId, AgentSectionKey section)
    {
        var cacheKey = $"agent_flag:{userId}:{section}";

        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        var flag = await _db.AgentFeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.SectionKey == section);

        if (flag == null)
        {
            _cache.Set(cacheKey, false, CacheTtl);
            return false;
        }

        // Check for user-level override
        var assignment = await _db.AgentFeatureFlagAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FeatureFlagId == flag.Id && a.UserId == userId);

        bool result = assignment?.IsEnabled ?? flag.IsEnabled;
        _cache.Set(cacheKey, result, CacheTtl);
        return result;
    }

    public async Task<bool> IsSectionAccessibleAsync(Guid userId, AgentSectionKey section)
    {
        // Master switch must be on
        if (!await IsFlagEnabledForUserAsync(userId, AgentSectionKey.AgentCore))
            return false;

        // If checking AgentCore itself, it's already true above
        if (section == AgentSectionKey.AgentCore)
            return true;

        return await IsFlagEnabledForUserAsync(userId, section);
    }

    public async Task<AgentConfigDto> GetAgentConfigForUserAsync(Guid userId)
    {
        var allFlags = await _db.AgentFeatureFlags
            .AsNoTracking()
            .ToListAsync();

        var allAssignments = await _db.AgentFeatureFlagAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var assignmentMap = allAssignments
            .ToDictionary(a => a.FeatureFlagId, a => a.IsEnabled);

        var sectionFlags = new Dictionary<string, bool>();
        bool agentCoreEnabled = false;

        foreach (var flag in allFlags)
        {
            bool effective = assignmentMap.TryGetValue(flag.Id, out var overrideVal)
                ? overrideVal
                : flag.IsEnabled;

            sectionFlags[flag.Key] = effective;

            if (flag.SectionKey == AgentSectionKey.AgentCore)
                agentCoreEnabled = effective;
        }

        string? activeProvider = null;
        if (agentCoreEnabled)
        {
            activeProvider = await _settings.GetAsync("DefaultProvider") ?? "OpenAI";
        }

        return new AgentConfigDto
        {
            AgentEnabled = agentCoreEnabled,
            SectionFlags = sectionFlags,
            ActiveProvider = activeProvider
        };
    }

    public async Task<AgentFeatureFlagDto> UpdateGlobalFlagAsync(AgentSectionKey section, bool isEnabled)
    {
        var flag = await _db.AgentFeatureFlags
            .FirstOrDefaultAsync(f => f.SectionKey == section)
            ?? throw new KeyNotFoundException($"Feature flag for section '{section}' not found.");

        flag.IsEnabled = isEnabled;
        flag.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        InvalidateCacheForSection(section);

        _logger.LogInformation("[AgentFlags] Global flag '{Section}' set to {Value}", section, isEnabled);
        return Map(flag);
    }

    public async Task SetUserOverrideAsync(Guid userId, AgentSectionKey section, bool isEnabled)
    {
        var flag = await _db.AgentFeatureFlags
            .FirstOrDefaultAsync(f => f.SectionKey == section)
            ?? throw new KeyNotFoundException($"Feature flag for section '{section}' not found.");

        var assignment = await _db.AgentFeatureFlagAssignments
            .FirstOrDefaultAsync(a => a.FeatureFlagId == flag.Id && a.UserId == userId);

        if (assignment == null)
        {
            assignment = new AgentFeatureFlagAssignment
            {
                FeatureFlagId = flag.Id,
                UserId = userId,
                IsEnabled = isEnabled
            };
            _db.AgentFeatureFlagAssignments.Add(assignment);
        }
        else
        {
            assignment.IsEnabled = isEnabled;
            assignment.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        _cache.Remove($"agent_flag:{userId}:{section}");
        _logger.LogInformation("[AgentFlags] User {UserId} override '{Section}' set to {Value}", userId, section, isEnabled);
    }

    public async Task RemoveUserOverrideAsync(Guid userId, AgentSectionKey section)
    {
        var flag = await _db.AgentFeatureFlags
            .FirstOrDefaultAsync(f => f.SectionKey == section);

        if (flag == null) return;

        var assignment = await _db.AgentFeatureFlagAssignments
            .FirstOrDefaultAsync(a => a.FeatureFlagId == flag.Id && a.UserId == userId);

        if (assignment == null) return;

        assignment.IsDeleted = true;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _cache.Remove($"agent_flag:{userId}:{section}");
    }

    public async Task<List<UserFeatureFlagOverrideDto>> GetUserOverridesAsync(Guid userId)
    {
        var results = await _db.AgentFeatureFlagAssignments
            .AsNoTracking()
            .Include(a => a.FeatureFlag)
            .Where(a => a.UserId == userId)
            .ToListAsync();

        return results.Select(a => new UserFeatureFlagOverrideDto
        {
            AssignmentId = a.Id,
            FlagKey = a.FeatureFlag.Key,
            FlagName = a.FeatureFlag.Name,
            IsEnabled = a.IsEnabled
        }).ToList();
    }

    private void InvalidateCacheForSection(AgentSectionKey section)
    {
        // Can't easily enumerate all user keys from IMemoryCache — on global flag change
        // we just let the 5-min TTL expire naturally, or restart triggers re-read.
        // For immediate effect, the SuperAdmin can wait ~5 min or restart the service.
    }

    private static AgentFeatureFlagDto Map(AgentFeatureFlag f) => new()
    {
        Id = f.Id,
        Key = f.Key,
        Name = f.Name,
        Description = f.Description,
        IsEnabled = f.IsEnabled,
        SectionKey = f.SectionKey.ToString()
    };
}
