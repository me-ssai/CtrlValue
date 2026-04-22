using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class PositionsController : EntityContextController
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _positionService = positionService;
    }

    /// <summary>
    /// Get all positions for the user's entity, optionally filtered by account
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PositionDto>>> GetPositions([FromQuery] Guid? accountId = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var positions = await _positionService.GetPositionsAsync(entityId, accountId);
        return Ok(positions);
    }

    /// <summary>
    /// Get a specific position by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PositionDto>> GetPositionById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var position = await _positionService.GetPositionByIdAsync(id, entityId);
        
        if (position == null)
            return NotFound(new { error = "Position not found or access denied." });

        return Ok(position);
    }

    /// <summary>
    /// Get position performance metrics
    /// </summary>
    [HttpGet("{id}/performance")]
    public async Task<ActionResult<PositionPerformanceDto>> GetPositionPerformance(Guid id)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            var performance = await _positionService.GetPositionPerformanceAsync(id, entityId);
            return Ok(performance);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get position current value
    /// </summary>
    [HttpGet("{id}/value")]
    public async Task<ActionResult<decimal?>> GetPositionValue(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var value = await _positionService.GetPositionValueAsync(id, entityId);
        
        if (value == null)
            return NotFound(new { error = "Position not found or access denied." });

        return Ok(new { value });
    }

    /// <summary>
    /// Create a new position
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PositionDto>> CreatePosition([FromBody] CreatePositionRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var position = await _positionService.CreatePositionAsync(request, entityId);
            return CreatedAtAction(nameof(GetPositionById), new { id = position.Id }, position);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing position
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PositionDto>> UpdatePosition(Guid id, [FromBody] UpdatePositionRequest request)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            var position = await _positionService.UpdatePositionAsync(id, request, entityId);
            return Ok(position);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a position (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePosition(Guid id)
    {
        try
        {
            var entityId = await ResolveEntityIdAsync();
            await RequirePermissionAsync(entityId, Permissions.Investments.Write);
            await _positionService.DeletePositionAsync(id, entityId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
