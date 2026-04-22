using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Exposes transaction intelligence: transfer detection, subscription analysis,
/// spending trends, merchant breakdowns, and cash-flow summaries.
/// </summary>
[ApiController]
[Route("api/intelligence")]
[Authorize]
public class IntelligenceController : EntityContextController
{
    private readonly ITransactionIntelligenceService _intelligence;
    private readonly ICategoryKeywordRuleService _ruleService;

    public IntelligenceController(
        ITransactionIntelligenceService intelligence,
        ICategoryKeywordRuleService ruleService,
        IEntityService entityService,
        IPermissionService permissions)
        : base(entityService, permissions)
    {
        _intelligence = intelligence;
        _ruleService = ruleService;
    }

    // ── Transfer Detection ───────────────────────────────────────────────────

    /// <summary>
    /// Returns candidate transfer pairs (unlinked outflow + inflow of matching amount).
    /// Sorted descending by confidence score.
    /// </summary>
    [HttpGet("transfers/candidates")]
    [ProducesResponseType(typeof(List<TransferCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransferCandidates([FromQuery] int lookbackDays = 7)
    {
        var entityId = await ResolveEntityIdAsync();
        var candidates = await _intelligence.DetectTransferCandidatesAsync(entityId, lookbackDays);
        return Ok(candidates);
    }

    /// <summary>
    /// Links two transactions as an internal transfer.
    /// Both rows get a shared TransferGroupId; types are re-set to Expense/Income.
    /// </summary>
    [HttpPost("transfers/link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkTransfer([FromBody] LinkTransferRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await _intelligence.LinkTransferAsync(request.OutflowTxnId, request.InflowTxnId, entityId);
        return NoContent();
    }

    /// <summary>
    /// Unlinks a transfer group — both legs revert to standalone transactions.
    /// </summary>
    [HttpDelete("transfers/{transferGroupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkTransfer(Guid transferGroupId)
    {
        var entityId = await ResolveEntityIdAsync();
        await _intelligence.UnlinkTransferAsync(transferGroupId, entityId);
        return NoContent();
    }

    // ── Subscriptions & Recurring ────────────────────────────────────────────

    /// <summary>
    /// Detects recurring payment patterns (subscriptions, standing orders).
    /// Grouped by normalised merchant + approximate amount + cadence.
    /// </summary>
    [HttpGet("recurring")]
    [ProducesResponseType(typeof(List<RecurringPatternDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecurringPatterns([FromQuery] int lookbackMonths = 6)
    {
        var entityId = await ResolveEntityIdAsync();
        var patterns = await _intelligence.DetectRecurringPatternsAsync(entityId, lookbackMonths);
        return Ok(patterns);
    }

    // ── Spending Analytics ───────────────────────────────────────────────────

    /// <summary>
    /// Month-over-month spending by category for the last N months.
    /// </summary>
    [HttpGet("spending/trend")]
    [ProducesResponseType(typeof(List<SpendingByMonthDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpendingTrend([FromQuery] int months = 6)
    {
        var entityId = await ResolveEntityIdAsync();
        var trend = await _intelligence.GetSpendingTrendAsync(entityId, months);
        return Ok(trend);
    }

    /// <summary>
    /// Top merchants by spend for the given date range.
    /// </summary>
    [HttpGet("spending/merchants")]
    [ProducesResponseType(typeof(List<MerchantSpendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopMerchants(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int topN = 10)
    {
        var entityId = await ResolveEntityIdAsync();
        var resolvedFrom = from ?? DateTime.UtcNow.AddMonths(-3);
        var resolvedTo   = to   ?? DateTime.UtcNow;
        var merchants = await _intelligence.GetTopMerchantsAsync(entityId, resolvedFrom, resolvedTo, topN);
        return Ok(merchants);
    }

    /// <summary>
    /// Income vs expenses per month (transfers excluded).
    /// </summary>
    [HttpGet("cashflow")]
    [ProducesResponseType(typeof(List<CashFlowMonthDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlow([FromQuery] int months = 6)
    {
        var entityId = await ResolveEntityIdAsync();
        var cashflow = await _intelligence.GetCashFlowAsync(entityId, months);
        return Ok(cashflow);
    }

    // ── Categorization ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies all keyword rules to uncategorized transactions in the workspace.
    /// Returns the number of transactions that were categorized.
    /// </summary>
    [HttpPost("categorization/apply-rules")]
    [ProducesResponseType(typeof(ApplyRulesResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyCategorizationRules()
    {
        var entityId = await ResolveEntityIdAsync();
        var count = await _ruleService.ApplyRulesToWorkspaceAsync(entityId);
        return Ok(new ApplyRulesResultDto { CategorizedCount = count });
    }

    /// <summary>
    /// Returns the best matching category for the given transaction description
    /// based on the workspace's keyword rules. Returns 204 if no match found.
    /// </summary>
    [HttpGet("categorization/suggest")]
    [ProducesResponseType(typeof(CategorySuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SuggestCategory([FromQuery] string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return NoContent();
        var entityId = await ResolveEntityIdAsync();
        var suggestion = await _ruleService.SuggestCategoryAsync(entityId, description);
        return suggestion == null ? NoContent() : Ok(suggestion);
    }
}

public record LinkTransferRequest(Guid OutflowTxnId, Guid InflowTxnId);
