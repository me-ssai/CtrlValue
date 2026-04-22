using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public class AccountsController : EntityContextController
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _accountService = accountService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountDto>>> GetAccounts([FromQuery] string? type = null)
    {
        var entityId = await ResolveEntityIdAsync();

        AccountType? accountType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<AccountType>(type, true, out var parsedType))
            accountType = parsedType;

        var accounts = await _accountService.GetAccountsAsync(entityId, accountType);
        return Ok(accounts);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> GetAccount(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var account = await _accountService.GetAccountByIdAsync(id, entityId);
        return Ok(account);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountDto>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Accounts.Write);
        var account = await _accountService.CreateAccountAsync(request, entityId);
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, account);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Accounts.Write);
        var account = await _accountService.UpdateAccountAsync(id, request, entityId);
        return Ok(account);
    }

    [HttpGet("{id}/deletion-impact")]
    [ProducesResponseType(typeof(AccountDeletionImpactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDeletionImpactDto>> GetDeletionImpact(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var impact = await _accountService.GetDeletionImpactAsync(id, entityId);
        return Ok(impact);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Accounts.Write);
        await _accountService.DeleteAccountAsync(id, entityId);
        return NoContent();
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(AccountSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountSummaryDto>> GetAccountSummary()
    {
        var entityId = await ResolveEntityIdAsync();
        var summary = await _accountService.GetAccountSummaryAsync(entityId);
        return Ok(summary);
    }

    [HttpPost("recalculate-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecalculateAllBalances()
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Accounts.Write);
        await _accountService.RecalculateAllBalancesAsync(entityId);
        return NoContent();
    }
}
