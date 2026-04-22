using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PriceHistoryController : ControllerBase
{
    private readonly IPriceHistoryService _priceHistoryService;

    public PriceHistoryController(IPriceHistoryService priceHistoryService)
    {
        _priceHistoryService = priceHistoryService;
    }

    [HttpGet("instrument/{instrumentId}")]
    public async Task<ActionResult<List<PriceHistoryDto>>> GetPriceHistory(Guid instrumentId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var prices = await _priceHistoryService.GetPriceHistoryAsync(instrumentId, startDate, endDate);
        return Ok(prices);
    }

    [HttpGet("instrument/{instrumentId}/latest")]
    public async Task<ActionResult<PriceHistoryDto>> GetLatestPrice(Guid instrumentId)
    {
        var price = await _priceHistoryService.GetLatestPriceAsync(instrumentId);
        if (price == null)
            return NotFound(new { error = "No price history found for this instrument." });
        return Ok(price);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PriceHistoryDto>> CreatePriceHistory([FromBody] CreatePriceHistoryRequest request)
    {
        try
        {
            var price = await _priceHistoryService.CreatePriceHistoryAsync(request);
            return CreatedAtAction(nameof(GetLatestPrice), new { instrumentId = price.InstrumentId }, price);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("bulk-import")]
    public async Task<ActionResult<object>> BulkImportPrices([FromBody] BulkPriceImportRequest request)
    {
        try
        {
            var count = await _priceHistoryService.BulkImportPricesAsync(request);
            return Ok(new { imported = count, message = $"Successfully imported {count} price records." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePriceHistory(Guid id)
    {
        try
        {
            await _priceHistoryService.DeletePriceHistoryAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

[Authorize]
[Route("api/[controller]")]
public class ValuationsController : EntityContextController
{
    private readonly IValuationService _valuationService;

    public ValuationsController(IValuationService valuationService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _valuationService = valuationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ValuationDto>>> GetValuations([FromQuery] Guid? accountId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var valuations = await _valuationService.GetValuationsAsync(entityId, accountId, startDate, endDate);
        return Ok(valuations);
    }

    [HttpGet("account/{accountId}/latest")]
    public async Task<ActionResult<ValuationDto>> GetLatestValuation(Guid accountId)
    {
        var entityId = await ResolveEntityIdAsync();
        var valuation = await _valuationService.GetLatestValuationAsync(accountId, entityId);
        if (valuation == null)
            return NotFound(new { error = "No valuations found for this account." });
        return Ok(valuation);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ValuationDto>> CreateValuation([FromBody] CreateValuationRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var valuation = await _valuationService.CreateValuationAsync(request, entityId);
            return CreatedAtAction(nameof(GetLatestValuation), new { accountId = valuation.AccountId }, valuation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ValuationDto>> UpdateValuation(Guid id, [FromBody] UpdateValuationRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var valuation = await _valuationService.UpdateValuationAsync(id, request, entityId);
            return Ok(valuation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteValuation(Guid id)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            await _valuationService.DeleteValuationAsync(id, entityId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

[Authorize]
[Route("api/[controller]")]
public class DepreciationSchedulesController : EntityContextController
{
    private readonly IDepreciationScheduleService _depreciationService;

    public DepreciationSchedulesController(IDepreciationScheduleService depreciationService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _depreciationService = depreciationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DepreciationScheduleDto>>> GetDepreciationSchedules()
    {
        var entityId = await ResolveEntityIdAsync();
        var schedules = await _depreciationService.GetDepreciationSchedulesAsync(entityId);
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepreciationScheduleDto>> GetDepreciationScheduleById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var schedule = await _depreciationService.GetDepreciationScheduleByIdAsync(id, entityId);
        if (schedule == null)
            return NotFound(new { error = "Depreciation schedule not found or access denied." });
        return Ok(schedule);
    }

    [HttpGet("account/{accountId}")]
    public async Task<ActionResult<DepreciationScheduleDto>> GetDepreciationScheduleByAccount(Guid accountId)
    {
        var entityId = await ResolveEntityIdAsync();
        var schedule = await _depreciationService.GetDepreciationScheduleByAccountAsync(accountId, entityId);
        if (schedule == null)
            return NotFound(new { error = "No depreciation schedule found for this account." });
        return Ok(schedule);
    }

    [HttpGet("{id}/current-value")]
    public async Task<ActionResult<object>> GetCurrentValue(Guid id, [FromQuery] DateTime? asOfDate = null)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            var value = await _depreciationService.CalculateCurrentValueAsync(id, entityId, asOfDate);
            return Ok(new { currentValue = value, asOfDate = asOfDate ?? DateTime.UtcNow });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DepreciationScheduleDto>> CreateDepreciationSchedule([FromBody] CreateDepreciationScheduleRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var schedule = await _depreciationService.CreateDepreciationScheduleAsync(request, entityId);
            return CreatedAtAction(nameof(GetDepreciationScheduleById), new { id = schedule.Id }, schedule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DepreciationScheduleDto>> UpdateDepreciationSchedule(Guid id, [FromBody] UpdateDepreciationScheduleRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var schedule = await _depreciationService.UpdateDepreciationScheduleAsync(id, request, entityId);
            return Ok(schedule);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDepreciationSchedule(Guid id)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            await _depreciationService.DeleteDepreciationScheduleAsync(id, entityId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

[Authorize]
[Route("api/[controller]")]
public class BudgetsController : EntityContextController
{
    private readonly IBudgetService _budgetService;

    public BudgetsController(IBudgetService budgetService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _budgetService = budgetService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BudgetDto>>> GetBudgets([FromQuery] Guid? categoryId = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var budgets = await _budgetService.GetBudgetsAsync(entityId, categoryId);
        return Ok(budgets);
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<BudgetDto>>> GetActiveBudgets([FromQuery] DateTime? asOfDate = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var budgets = await _budgetService.GetActiveBudgetsAsync(entityId, asOfDate);
        return Ok(budgets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BudgetDto>> GetBudgetById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var budget = await _budgetService.GetBudgetByIdAsync(id, entityId);
        if (budget == null)
            return NotFound(new { error = "Budget not found or access denied." });
        return Ok(budget);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BudgetDto>> CreateBudget([FromBody] CreateBudgetRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Budgets.Write);
            var budget = await _budgetService.CreateBudgetAsync(request, entityId);
            return CreatedAtAction(nameof(GetBudgetById), new { id = budget.Id }, budget);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<BudgetDto>> UpdateBudget(Guid id, [FromBody] UpdateBudgetRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Budgets.Write);
            var budget = await _budgetService.UpdateBudgetAsync(id, request, entityId);
            return Ok(budget);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBudget(Guid id)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Budgets.Write);
            await _budgetService.DeleteBudgetAsync(id, entityId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
