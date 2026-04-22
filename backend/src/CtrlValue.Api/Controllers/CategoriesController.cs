using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Api.Controllers;

[Route("api/[controller]")]
[Authorize]
public class CategoriesController : EntityContextController
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] string? type = null)
    {
        var entityId = await ResolveEntityIdAsync();

        CategoryType? categoryType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<CategoryType>(type, true, out var parsedType))
            categoryType = parsedType;

        var categories = await _categoryService.GetCategoriesAsync(entityId, categoryType);
        return Ok(categories);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetCategory(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var category = await _categoryService.GetCategoryByIdAsync(id, entityId);
        return Ok(category);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var category = await _categoryService.CreateCategoryAsync(request, entityId);
        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        var category = await _categoryService.UpdateCategoryAsync(id, request, entityId);
        return Ok(category);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Transactions.Write);
        await _categoryService.DeleteCategoryAsync(id, entityId);
        return NoContent();
    }
}
