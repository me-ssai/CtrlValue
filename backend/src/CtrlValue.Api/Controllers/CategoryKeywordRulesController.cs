using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public class CategoryKeywordRulesController : EntityContextController
{
    private readonly ICategoryKeywordRuleService _ruleService;

    public CategoryKeywordRulesController(ICategoryKeywordRuleService ruleService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _ruleService = ruleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryKeywordRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryKeywordRuleDto>>> GetAll()
    {
        var entityId = await ResolveEntityIdAsync();
        var rules = await _ruleService.GetAllAsync(entityId);
        return Ok(rules);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryKeywordRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryKeywordRuleDto>> GetById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var rule = await _ruleService.GetByIdAsync(entityId, id);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    [HttpGet("category/{categoryId}")]
    [ProducesResponseType(typeof(List<CategoryKeywordRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryKeywordRuleDto>>> GetByCategory(Guid categoryId)
    {
        var entityId = await ResolveEntityIdAsync();
        var rules = await _ruleService.GetByCategoryAsync(entityId, categoryId);
        return Ok(rules);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryKeywordRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryKeywordRuleDto>> Create([FromBody] CreateCategoryKeywordRuleRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdStr, out var id) ? id : null;

        var rule = await _ruleService.CreateAsync(entityId, request, userId);
        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CategoryKeywordRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryKeywordRuleDto>> Update(Guid id, [FromBody] UpdateCategoryKeywordRuleRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var rule = await _ruleService.UpdateAsync(entityId, id, request);
        return Ok(rule);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _ruleService.DeleteAsync(entityId, id);
        return NoContent();
    }
}
