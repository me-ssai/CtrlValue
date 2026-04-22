using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CtrlValue.Api.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Provides read-only demo data endpoints for the public demo tenant.
/// All endpoints are anonymous — no real user authentication is required.
/// Access is gated server-side by <see cref="DemoRequestContext.IsDemo"/>,
/// which is set only when the request originates from the demo origin.
/// </summary>
[ApiController]
[Route("api/demo")]
[AllowAnonymous]
[EnableRateLimiting("demo")]
public class DemoController : ControllerBase
{
    private readonly DemoRequestContext _demoCtx;
    private readonly IAccountService _accounts;
    private readonly ITransactionService _transactions;
    private readonly ICategoryService _categories;
    private readonly IBudgetService _budgets;
    private readonly IPositionService _positions;
    private readonly IInstrumentService _instruments;

    public DemoController(
        DemoRequestContext demoCtx,
        IAccountService accounts,
        ITransactionService transactions,
        ICategoryService categories,
        IBudgetService budgets,
        IPositionService positions,
        IInstrumentService instruments)
    {
        _demoCtx      = demoCtx;
        _accounts     = accounts;
        _transactions = transactions;
        _categories   = categories;
        _budgets      = budgets;
        _positions    = positions;
        _instruments  = instruments;
    }

    /// <summary>
    /// Returns whether the current request is operating in demo mode.
    /// The frontend calls this on bootstrap to confirm server-side demo detection.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new DemoConfigDto
        {
            IsDemoMode = _demoCtx.IsDemo,
            EntityId   = _demoCtx.IsDemo ? DemoConstants.DemoEntityId.ToString() : null,
            EntityName = _demoCtx.IsDemo ? DemoConstants.DemoEntityName : null,
        });
    }

    /// <summary>
    /// Returns the full bootstrap payload for the demo session — all seeded accounts,
    /// recent transactions, categories, budgets, positions, and instruments in one call.
    /// Only available when the request originates from the configured demo origin.
    /// </summary>
    [HttpGet("bootstrap")]
    public async Task<IActionResult> GetBootstrap()
    {
        if (!_demoCtx.IsDemo)
            return StatusCode(403, new { message = "This endpoint is only available in demo mode." });

        var eid = DemoConstants.DemoEntityId;
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);

        var accounts     = await _accounts.GetAccountsAsync(eid);
        var transactions = await _transactions.GetTransactionsAsync(eid, startDate: sixMonthsAgo);
        var categories   = await _categories.GetCategoriesAsync(eid);
        var budgets      = await _budgets.GetBudgetsAsync(eid);
        var positions    = await _positions.GetPositionsAsync(eid);
        var instruments  = await _instruments.GetInstrumentsAsync();

        // Only return instruments that are referenced by demo positions
        var positionInstrumentIds = positions
            .Where(p => p.InstrumentId.HasValue)
            .Select(p => p.InstrumentId!.Value)
            .ToHashSet();
        var demoInstruments = instruments
            .Where(i => positionInstrumentIds.Contains(i.Id))
            .ToList();

        var bootstrap = new DemoBootstrapDto
        {
            Entity = new EntityDto
            {
                Id           = eid,
                Name         = DemoConstants.DemoEntityName,
                BaseCurrency = "AUD",
                Country      = "AU",
                TenantId     = DemoConstants.DemoTenantId,
            },
            Accounts         = accounts,
            RecentTransactions = transactions,
            Categories       = categories,
            Budgets          = budgets,
            Positions        = positions,
            Instruments      = demoInstruments,
        };

        return Ok(bootstrap);
    }
}
