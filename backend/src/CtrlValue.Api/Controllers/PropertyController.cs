using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class PropertyController : EntityContextController
{
    private readonly IPropertyService _propertyService;

    public PropertyController(IPropertyService propertyService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _propertyService = propertyService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PropertyDto>>> GetProperties()
    {
        var entityId = await ResolveEntityIdAsync();
        var properties = await _propertyService.GetPropertiesAsync(entityId);
        return Ok(properties);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyDto>> GetPropertyById(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        var property = await _propertyService.GetPropertyByIdAsync(id, entityId);
        return property == null ? NotFound() : Ok(property);
    }

    [HttpGet("account/{accountId:guid}")]
    public async Task<ActionResult<PropertyDto>> GetPropertyByAccount(Guid accountId)
    {
        var entityId = await ResolveEntityIdAsync();
        var property = await _propertyService.GetPropertyByAccountIdAsync(accountId, entityId);
        return property == null ? NotFound() : Ok(property);
    }

    [HttpPost]
    public async Task<ActionResult<PropertyDto>> CreateProperty([FromBody] CreatePropertyRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Write);
        request.EntityId = entityId;
        var property = await _propertyService.CreatePropertyAsync(request, entityId);
        return CreatedAtAction(nameof(GetPropertyById), new { id = property.Id }, property);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PropertyDto>> UpdateProperty(Guid id, [FromBody] UpdatePropertyRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Write);
        try
        {
            var property = await _propertyService.UpdatePropertyAsync(id, request, entityId);
            return Ok(property);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProperty(Guid id)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Write);
        try
        {
            await _propertyService.DeletePropertyAsync(id, entityId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
