using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Services;

namespace CtrlValue.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/entities/{entityId}/loans")]
public class LoanController : ControllerBase
{
    private readonly ILoanService _loanService;

    public LoanController(ILoanService loanService)
    {
        _loanService = loanService;
    }

    /// <summary>Get all loan details for the entity (used by the Loans dashboard tab).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<LoanDetailsDto>), 200)]
    public async Task<IActionResult> GetAll(Guid entityId)
    {
        var loans = await _loanService.GetAllLoansByEntityAsync(entityId);
        return Ok(loans);
    }

    /// <summary>Get loan details for a specific account.</summary>
    [HttpGet("{accountId}")]
    [ProducesResponseType(typeof(LoanDetailsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByAccount(Guid entityId, Guid accountId)
    {
        var loan = await _loanService.GetLoanDetailsByAccountAsync(accountId, entityId);
        return loan == null ? NotFound() : Ok(loan);
    }

    /// <summary>Create loan details for a LIABILITY account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LoanDetailsDto), 201)]
    public async Task<IActionResult> Create(Guid entityId, [FromBody] CreateLoanDetailsRequest request)
    {
        var result = await _loanService.CreateLoanDetailsAsync(request, entityId);
        return CreatedAtAction(nameof(GetByAccount), new { entityId, accountId = result.AccountId }, result);
    }

    /// <summary>Update loan details.</summary>
    [HttpPut("{loanId}")]
    [ProducesResponseType(typeof(LoanDetailsDto), 200)]
    public async Task<IActionResult> Update(Guid entityId, Guid loanId, [FromBody] UpdateLoanDetailsRequest request)
    {
        var result = await _loanService.UpdateLoanDetailsAsync(loanId, request, entityId);
        return Ok(result);
    }

    /// <summary>Get the loan summary card data (remaining balance, LVR, next payment, etc.).</summary>
    [HttpGet("{accountId}/summary")]
    [ProducesResponseType(typeof(LoanSummaryDto), 200)]
    public async Task<IActionResult> GetSummary(Guid entityId, Guid accountId)
    {
        var summary = await _loanService.GetLoanSummaryAsync(accountId, entityId);
        return Ok(summary);
    }

    /// <summary>Get the full amortisation schedule. Pass extraPayment query param for accelerated projection.</summary>
    [HttpGet("{accountId}/schedule")]
    [ProducesResponseType(typeof(AmortisationScheduleDto), 200)]
    public async Task<IActionResult> GetSchedule(Guid entityId, Guid accountId, [FromQuery] decimal extraPayment = 0)
    {
        var schedule = await _loanService.GetAmortisationScheduleAsync(accountId, entityId, extraPayment);
        return Ok(schedule);
    }

    /// <summary>Record a rate change (adds to rate history, updates current rate).</summary>
    [HttpPost("{loanId}/rate-change")]
    [ProducesResponseType(typeof(LoanDetailsDto), 200)]
    public async Task<IActionResult> AddRateChange(Guid entityId, Guid loanId, [FromBody] LoanRateChangeRequest request)
    {
        var result = await _loanService.AddRateChangeAsync(loanId, request, entityId);
        return Ok(result);
    }

    /// <summary>Recalculate redraw available from extra repayment transactions.</summary>
    [HttpPost("{accountId}/recalculate-redraw")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RecalculateRedraw(Guid entityId, Guid accountId)
    {
        await _loanService.RecalculateRedrawAsync(accountId, entityId);
        return NoContent();
    }
}
