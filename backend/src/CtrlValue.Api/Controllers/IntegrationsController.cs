using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Authorize]
[Route("api/integrations")]
public class IntegrationsController : EntityContextController
{
    private readonly IEntityIntegrationService _integrationService;

    public IntegrationsController(
        IEntityService entityService,
        IPermissionService permissions,
        IEntityIntegrationService integrationService)
        : base(entityService, permissions)
    {
        _integrationService = integrationService;
    }

    /// <summary>GET /api/integrations — list all integration configs for the current entity (API keys redacted).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Read);

        var integrations = await _integrationService.GetIntegrationsAsync(entityId);
        return Ok(integrations);
    }

    /// <summary>GET /api/integrations/{type} — get a single integration config.</summary>
    [HttpGet("{type}")]
    public async Task<IActionResult> GetOne(string type)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Read);

        var integration = await _integrationService.GetIntegrationAsync(entityId, type.ToUpperInvariant());
        if (integration == null) return NotFound();
        return Ok(integration);
    }

    /// <summary>PUT /api/integrations/{type} — upsert API key and enable/disable.</summary>
    [HttpPut("{type}")]
    public async Task<IActionResult> Upsert(string type, [FromBody] UpsertIntegrationRequest request)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Write);

        var entity = await EntityService.GetEntityByIdAsync(entityId, GetUserId())
            ?? throw new KeyNotFoundException("Entity not found.");

        var integration = await _integrationService.UpsertIntegrationAsync(
            entityId,
            entity.TenantId,
            type.ToUpperInvariant(),
            request.ApiKey,
            request.IsEnabled);

        return Ok(integration);
    }

    /// <summary>DELETE /api/integrations/{type} — disable and clear API key.</summary>
    [HttpDelete("{type}")]
    public async Task<IActionResult> Delete(string type)
    {
        var entityId = await ResolveEntityIdAsync();
        await RequirePermissionAsync(entityId, Permissions.Investments.Write);

        await _integrationService.DeleteIntegrationAsync(entityId, type.ToUpperInvariant());
        return NoContent();
    }
}

public record UpsertIntegrationRequest(
    /// <summary>The API key to store. Omit (null) to leave existing key unchanged.</summary>
    string? ApiKey,
    bool IsEnabled
);
