using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Api.Infrastructure;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Base controller that resolves the active entity from the X-Entity-Id request header.
/// Falls back to the user's default entity when the header is absent.
/// All controllers that need entity-scoped data should inherit from this.
/// </summary>
[ApiController]
public abstract class EntityContextController : ControllerBase
{
    protected readonly IEntityService EntityService;
    protected readonly IPermissionService PermissionSvc;

    protected EntityContextController(IEntityService entityService, IPermissionService permissions)
    {
        EntityService = entityService;
        PermissionSvc = permissions;
    }

    protected Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// Returns the entity ID to scope the current request to.
    /// Priority: X-Entity-Id header → user's default entity.
    /// Validates that the user actually has access to the requested entity.
    /// </summary>
    protected async Task<Guid> ResolveEntityIdAsync()
    {
        // Demo requests are always scoped to the well-known demo entity — no user resolution needed.
        var demoCtx = HttpContext.RequestServices.GetRequiredService<DemoRequestContext>();
        if (demoCtx.IsDemo)
            return DemoConstants.DemoEntityId;

        var userId = GetUserId();

        if (Request.Headers.TryGetValue("X-Entity-Id", out var headerValue) &&
            Guid.TryParse(headerValue.FirstOrDefault(), out var requestedEntityId))
        {
            // Validate user has access to this entity
            var entity = await EntityService.GetEntityByIdAsync(requestedEntityId, userId);
            if (entity != null)
                return requestedEntityId;

            // Header present but user doesn't have access — fall through to default
        }

        var defaultEntity = await EntityService.GetOrCreateDefaultEntityAsync(userId);
        return defaultEntity.Id;
    }

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the current user does not hold
    /// <paramref name="permission"/> within <paramref name="entityId"/>.
    /// </summary>
    protected Task RequirePermissionAsync(Guid entityId, string permission) =>
        PermissionSvc.RequireAsync(GetUserId(), entityId, permission);
}
