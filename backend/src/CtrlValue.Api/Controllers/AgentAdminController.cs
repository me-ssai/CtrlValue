using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Super-Admin-only endpoints for managing agent feature flags, user overrides, and audit logs.
/// Does NOT inherit EntityContextController — no entity scoping needed here.
/// </summary>
[ApiController]
[Route("api/admin/agent")]
[Authorize(Policy = "SuperAdmin")]
public class AgentAdminController : ControllerBase
{
    private readonly IAgentFeatureFlagService _flags;
    private readonly IAgentAuditService _audit;
    private readonly IAgentSettingService _settings;
    private readonly IAgentDigestService _digest;

    public AgentAdminController(
        IAgentFeatureFlagService flags,
        IAgentAuditService audit,
        IAgentSettingService settings,
        IAgentDigestService digest)
    {
        _flags = flags;
        _audit = audit;
        _settings = settings;
        _digest = digest;
    }

    // ── Global Feature Flags ─────────────────────────────────────────────────

    /// <summary>Returns all 9 agent feature flags with their global enabled state.</summary>
    [HttpGet("features")]
    public async Task<ActionResult<List<AgentFeatureFlagDto>>> GetAllFlags()
    {
        var flags = await _flags.GetAllFlagsAsync();
        return Ok(flags);
    }

    /// <summary>Updates the global enabled state for a specific agent section.</summary>
    [HttpPut("features/{sectionKey}")]
    public async Task<ActionResult<AgentFeatureFlagDto>> UpdateFlag(
        string sectionKey,
        [FromBody] UpdateFeatureFlagRequest request)
    {
        if (!Enum.TryParse<AgentSectionKey>(sectionKey, ignoreCase: true, out var section))
            return BadRequest($"Unknown section key: '{sectionKey}'");

        var result = await _flags.UpdateGlobalFlagAsync(section, request.IsEnabled);
        return Ok(result);
    }

    // ── User Overrides ───────────────────────────────────────────────────────

    /// <summary>Returns all flag overrides for a specific user.</summary>
    [HttpGet("users/{userId:guid}/overrides")]
    public async Task<ActionResult<List<UserFeatureFlagOverrideDto>>> GetUserOverrides(Guid userId)
    {
        var overrides = await _flags.GetUserOverridesAsync(userId);
        return Ok(overrides);
    }

    /// <summary>Sets a per-user override for a specific section.</summary>
    [HttpPut("users/{userId:guid}/overrides/{sectionKey}")]
    public async Task<IActionResult> SetUserOverride(
        Guid userId,
        string sectionKey,
        [FromBody] UpdateFeatureFlagRequest request)
    {
        if (!Enum.TryParse<AgentSectionKey>(sectionKey, ignoreCase: true, out var section))
            return BadRequest($"Unknown section key: '{sectionKey}'");

        await _flags.SetUserOverrideAsync(userId, section, request.IsEnabled);
        return NoContent();
    }

    /// <summary>Removes the per-user override for a section (reverts to global flag).</summary>
    [HttpDelete("users/{userId:guid}/overrides/{sectionKey}")]
    public async Task<IActionResult> RemoveUserOverride(Guid userId, string sectionKey)
    {
        if (!Enum.TryParse<AgentSectionKey>(sectionKey, ignoreCase: true, out var section))
            return BadRequest($"Unknown section key: '{sectionKey}'");

        await _flags.RemoveUserOverrideAsync(userId, section);
        return NoContent();
    }

    // ── Agent Settings ───────────────────────────────────────────────────────

    /// <summary>Returns all agent settings as a key-value dictionary.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<Dictionary<string, string>>> GetSettings()
    {
        var settings = await _settings.GetAllAsync();
        return Ok(settings);
    }

    /// <summary>Sets (upserts) an agent setting by key.</summary>
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> SetSetting(string key, [FromBody] UpdateAgentSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(request.Value))
            return BadRequest("Key and value are required.");

        await _settings.SetAsync(key, request.Value.Trim());
        return NoContent();
    }

    // ── Weekly Digests ───────────────────────────────────────────────────────

    /// <summary>Returns all pending digest emails awaiting approval.</summary>
    [HttpGet("digests/pending")]
    public async Task<ActionResult<List<AgentDigestEmailDto>>> GetPendingDigests()
    {
        var result = await _digest.GetPendingDigestsAsync();
        return Ok(result);
    }

    /// <summary>Returns all digests (paged).</summary>
    [HttpGet("digests")]
    public async Task<ActionResult<List<AgentDigestEmailDto>>> GetAllDigests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _digest.GetAllDigestsAsync(page, pageSize);
        return Ok(result);
    }

    /// <summary>Approves a digest email for sending.</summary>
    [HttpPost("digests/{digestId:guid}/approve")]
    public async Task<IActionResult> ApproveDigest(Guid digestId)
    {
        var adminUserId = GetAdminUserId();
        await _digest.ApproveDigestAsync(digestId, adminUserId);
        return NoContent();
    }

    /// <summary>Rejects a digest email.</summary>
    [HttpPost("digests/{digestId:guid}/reject")]
    public async Task<IActionResult> RejectDigest(Guid digestId)
    {
        var adminUserId = GetAdminUserId();
        await _digest.RejectDigestAsync(digestId, adminUserId);
        return NoContent();
    }

    private Guid GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                   ?? User.FindFirst("sub")
                   ?? throw new UnauthorizedAccessException("User identity not found.");
        return Guid.Parse(claim.Value);
    }

    // ── Audit Logs ───────────────────────────────────────────────────────────

    /// <summary>Returns paginated agent audit logs.</summary>
    [HttpGet("audit")]
    public async Task<ActionResult<List<AgentAuditLogDto>>> GetAuditLogs(
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 200) pageSize = 200;

        var logs = await _audit.GetAuditLogsAsync(userId, page, pageSize);
        return Ok(logs);
    }
}
