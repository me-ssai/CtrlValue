using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EntitiesController : ControllerBase
{
    private readonly IEntityService _entityService;
    private readonly IPermissionService _permissions;

    public EntitiesController(IEntityService entityService, IPermissionService permissions)
    {
        _entityService = entityService;
        _permissions   = permissions;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim!);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity Endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all entities the current user has access to
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EntityDto>>> GetUserEntities()
    {
        var userId = GetUserId();
        var entities = await _entityService.GetUserEntitiesAsync(userId);
        return Ok(entities);
    }

    /// <summary>
    /// Get or create the default entity for the current user
    /// </summary>
    [HttpGet("default")]
    public async Task<ActionResult<EntityDto>> GetDefaultEntity()
    {
        var userId = GetUserId();
        var entity = await _entityService.GetOrCreateDefaultEntityAsync(userId);
        return Ok(entity);
    }

    /// <summary>
    /// Get a specific entity by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<EntityDto>> GetEntityById(Guid id)
    {
        var userId = GetUserId();
        var entity = await _entityService.GetEntityByIdAsync(id, userId);
        
        if (entity == null)
            return NotFound(new { error = "Entity not found or access denied." });

        return Ok(entity);
    }

    /// <summary>
    /// Create a new entity
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EntityDto>> CreateEntity([FromBody] CreateEntityRequest request)
    {
        try
        {
            var userId = GetUserId();
            var entity = await _entityService.CreateEntityAsync(request, userId);
            return CreatedAtAction(nameof(GetEntityById), new { id = entity.Id }, entity);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing entity
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<EntityDto>> UpdateEntity(Guid id, [FromBody] UpdateEntityRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _permissions.RequireAsync(userId, id, Permissions.Entity.Manage);
            var entity = await _entityService.UpdateEntityAsync(id, request, userId);
            return Ok(entity);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Delete an entity (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntity(Guid id)
    {
        try
        {
            var userId = GetUserId();
            await _permissions.RequireAsync(userId, id, Permissions.Entity.Manage);
            await _entityService.DeleteEntityAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EntityUser Management Endpoints
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all users with access to an entity
    /// </summary>
    [HttpGet("{id}/users")]
    public async Task<ActionResult<List<EntityUserDto>>> GetEntityUsers(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var users = await _entityService.GetEntityUsersAsync(id, userId);
            return Ok(users);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>
    /// Add a user to an entity
    /// </summary>
    [HttpPost("{id}/users")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EntityUserDto>> AddUserToEntity(Guid id, [FromBody] AddEntityUserRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _permissions.RequireAsync(userId, id, Permissions.Members.Manage);
            var entityUser = await _entityService.AddUserToEntityAsync(id, request, userId);
            return CreatedAtAction(nameof(GetEntityUsers), new { id }, entityUser);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a user's role in an entity
    /// </summary>
    [HttpPut("{id}/users/{userId}")]
    public async Task<ActionResult<EntityUserDto>> UpdateEntityUserRole(Guid id, Guid userId, [FromBody] UpdateEntityUserRequest request)
    {
        try
        {
            var currentUserId = GetUserId();
            await _permissions.RequireAsync(currentUserId, id, Permissions.Members.Manage);
            var entityUser = await _entityService.UpdateEntityUserRoleAsync(id, userId, request, currentUserId);
            return Ok(entityUser);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a user from an entity
    /// </summary>
    [HttpDelete("{id}/users/{userId}")]
    public async Task<IActionResult> RemoveUserFromEntity(Guid id, Guid userId)
    {
        try
        {
            var currentUserId = GetUserId();
            await _permissions.RequireAsync(currentUserId, id, Permissions.Members.Manage);
            await _entityService.RemoveUserFromEntityAsync(id, userId, currentUserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
