using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Manages custom roles for a workspace (entity).
/// Requires entity:manage permission for write operations.
/// </summary>
[Authorize]
[Route("api/entities/{entityId}/roles")]
public class EntityRolesController : EntityContextController
{
    public EntityRolesController(IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions) { }

    [HttpGet]
    [ProducesResponseType(typeof(List<EntityCustomRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EntityCustomRoleDto>>> GetRoles(Guid entityId)
    {
        // Any member of the entity can list roles
        var entity = await EntityService.GetEntityByIdAsync(entityId, GetUserId());
        if (entity == null) return NotFound();

        var roles = await EntityService.GetEntityRolesAsync(entityId);
        return Ok(roles);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EntityCustomRoleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EntityCustomRoleDto>> CreateRole(Guid entityId, [FromBody] CreateEntityCustomRoleRequest request)
    {
        await RequirePermissionAsync(entityId, Permissions.Entity.Manage);
        try
        {
            var role = await EntityService.CreateEntityRoleAsync(entityId, request);
            return CreatedAtAction(nameof(GetRoles), new { entityId }, role);
        }
        catch (ArgumentException ex)    { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPut("{roleId}")]
    [ProducesResponseType(typeof(EntityCustomRoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntityCustomRoleDto>> UpdateRole(Guid entityId, Guid roleId, [FromBody] UpdateEntityCustomRoleRequest request)
    {
        await RequirePermissionAsync(entityId, Permissions.Entity.Manage);
        try
        {
            var role = await EntityService.UpdateEntityRoleAsync(entityId, roleId, request);
            return Ok(role);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{roleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRole(Guid entityId, Guid roleId)
    {
        await RequirePermissionAsync(entityId, Permissions.Entity.Manage);
        try
        {
            await EntityService.DeleteEntityRoleAsync(entityId, roleId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
