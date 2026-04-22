using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
public class AgentController : EntityContextController
{
    private readonly IAgentFeatureFlagService _flags;
    private readonly IAgentContextBuilderService _contextBuilder;
    private readonly IAgentOrchestratorService _orchestrator;
    private readonly IAgentInsightService _insights;
    private readonly IAgentWebResearchService _webResearch;
    private readonly IAgentScenarioService _scenarios;
    private readonly IAgentSavingsHistoryService _savingsHistory;

    public AgentController(
        IEntityService entityService,
        IPermissionService permissions,
        IAgentFeatureFlagService flags,
        IAgentContextBuilderService contextBuilder,
        IAgentOrchestratorService orchestrator,
        IAgentInsightService insights,
        IAgentWebResearchService webResearch,
        IAgentScenarioService scenarios,
        IAgentSavingsHistoryService savingsHistory)
        : base(entityService, permissions)
    {
        _flags = flags;
        _contextBuilder = contextBuilder;
        _orchestrator = orchestrator;
        _insights = insights;
        _webResearch = webResearch;
        _scenarios = scenarios;
        _savingsHistory = savingsHistory;
    }

    // ── Config ───────────────────────────────────────────────────────────────

    /// <summary>Returns the effective agent configuration for the current user.</summary>
    [HttpGet("config")]
    public async Task<ActionResult<AgentConfigDto>> GetConfig()
    {
        var userId = GetUserId();
        var config = await _flags.GetAgentConfigForUserAsync(userId);
        return Ok(config);
    }

    // ── Context ──────────────────────────────────────────────────────────────

    /// <summary>Returns the current finance context snapshot for the active entity.</summary>
    [HttpGet("context/summary")]
    public async Task<ActionResult<FinanceContextDto>> GetContextSummary(
        [FromQuery] bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.PersonalFinance))
            return Forbid();

        await RequirePermissionAsync(entityId, Permissions.Agent.Read);

        var ctx = await _contextBuilder.BuildContextAsync(userId, entityId, forceRefresh, ct);
        return Ok(ctx);
    }

    // ── Chat ─────────────────────────────────────────────────────────────────

    /// <summary>Sends a chat message. Returns user + assistant messages.</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<AgentChatResponse>> Chat(
        [FromBody] SendMessageRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ConversationalChat))
            return Forbid();

        await RequirePermissionAsync(entityId, Permissions.Agent.Chat);

        var response = await _orchestrator.ChatAsync(userId, entityId, request, ct);
        return Ok(response);
    }

    /// <summary>Lists conversations for the current user and entity.</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<List<AgentConversationDto>>> GetConversations()
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ConversationalChat))
            return Forbid();

        var conversations = await _orchestrator.GetConversationsAsync(userId, entityId);
        return Ok(conversations);
    }

    /// <summary>Returns messages for a specific conversation.</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<List<AgentMessageDto>>> GetMessages(Guid conversationId)
    {
        var userId = GetUserId();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ConversationalChat))
            return Forbid();

        var messages = await _orchestrator.GetMessagesAsync(conversationId, userId);
        return Ok(messages);
    }

    // ── Insights ─────────────────────────────────────────────────────────────

    /// <summary>Returns active (non-dismissed) insights for the current entity.</summary>
    [HttpGet("insights")]
    public async Task<ActionResult<List<AgentInsightDto>>> GetInsights()
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.NetWorthAnalysis))
            return Forbid();

        var result = await _insights.GetInsightsAsync(userId, entityId);
        return Ok(result);
    }

    /// <summary>Refreshes rule-based insights for the current entity.</summary>
    [HttpPost("insights/refresh")]
    public async Task<IActionResult> RefreshInsights(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.NetWorthAnalysis))
            return Forbid();

        await _insights.RefreshInsightsAsync(userId, entityId, ct);
        return NoContent();
    }

    /// <summary>Dismisses a specific insight.</summary>
    [HttpPost("insights/{insightId:guid}/dismiss")]
    public async Task<IActionResult> DismissInsight(Guid insightId)
    {
        var userId = GetUserId();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.NetWorthAnalysis))
            return Forbid();

        await _insights.DismissInsightAsync(insightId, userId);
        return NoContent();
    }

    // ── Macro ─────────────────────────────────────────────────────────────────

    /// <summary>Returns all macro economic summaries (cached or fresh).</summary>
    [HttpGet("macro")]
    public async Task<ActionResult<List<MacroSummaryDto>>> GetMacroSummaries(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.MacroInsights))
            return Forbid();

        var result = await _webResearch.GetAllMacroSummariesAsync(entityId, ct);
        return Ok(result);
    }

    /// <summary>Returns the macro summary for a specific topic.</summary>
    [HttpGet("macro/{topicKey}")]
    public async Task<ActionResult<MacroSummaryDto>> GetMacroSummary(
        string topicKey,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.MacroInsights))
            return Forbid();

        var result = await _webResearch.GetMacroSummaryAsync(topicKey, entityId, ct);
        return Ok(result);
    }

    // ── Savings History ───────────────────────────────────────────────────────

    /// <summary>Returns monthly savings rate snapshots for trend charting.</summary>
    [HttpGet("savings-history")]
    public async Task<ActionResult<List<SavingsSnapshotDto>>> GetSavingsHistory(
        [FromQuery] int months = 24)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.PersonalFinance))
            return Forbid();

        var history = await _savingsHistory.GetHistoryAsync(entityId, months);
        return Ok(history);
    }

    // ── Scenarios ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a what-if scenario and returns calculated results.
    /// Supported types: CutCategory, PayOffLoan, IncreaseSavingsRate, SellVehicle.
    /// </summary>
    [HttpPost("scenarios/run")]
    public async Task<ActionResult<ScenarioResultDto>> RunScenario(
        [FromBody] RunScenarioRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ScenarioExploration))
            return Forbid();

        await RequirePermissionAsync(entityId, Permissions.Agent.Read);

        try
        {
            var result = await _scenarios.RunScenarioAsync(userId, entityId, request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Returns the scenario run history for the current entity.</summary>
    [HttpGet("scenarios")]
    public async Task<ActionResult<List<AgentScenarioHistoryDto>>> GetScenarioHistory(
        [FromQuery] int limit = 20)
    {
        var userId = GetUserId();
        var entityId = await ResolveEntityIdAsync();

        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ScenarioExploration))
            return Forbid();

        var history = await _scenarios.GetScenarioHistoryAsync(userId, entityId, Math.Min(limit, 50));
        return Ok(history);
    }
}
