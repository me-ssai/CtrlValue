using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public class AccountKeywordRulesController : EntityContextController
{
    private readonly IAccountKeywordRuleService _ruleService;

    public AccountKeywordRulesController(IAccountKeywordRuleService ruleService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _ruleService = ruleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AccountKeywordRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountKeywordRuleDto>>> GetAll()
    {
        var entityId = await ResolveEntityIdAsync();
        var rules = await _ruleService.GetAllAsync(entityId);
        return Ok(rules);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AccountKeywordRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountKeywordRuleDto>> GetById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var rule = await _ruleService.GetByIdAsync(entityId, id);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    [HttpGet("account/{accountId}")]
    [ProducesResponseType(typeof(List<AccountKeywordRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AccountKeywordRuleDto>>> GetByAccount(Guid accountId)
    {
        var entityId = await ResolveEntityIdAsync();
        var rules = await _ruleService.GetByAccountAsync(entityId, accountId);
        return Ok(rules);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountKeywordRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountKeywordRuleDto>> Create([FromBody] CreateAccountKeywordRuleRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdStr, out var id) ? id : null;

        var rule = await _ruleService.CreateAsync(entityId, request, userId);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AccountKeywordRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountKeywordRuleDto>> Update(Guid id, [FromBody] UpdateAccountKeywordRuleRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var rule = await _ruleService.UpdateAsync(entityId, id, request);
        return Ok(rule);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _ruleService.DeleteAsync(entityId, id);
        return NoContent();
    }
}
