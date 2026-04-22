using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;

namespace CtrlValue.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class DashboardController : EntityContextController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService, IEntityService entityService, IPermissionService permissions)
        : base(entityService, permissions)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Get dashboard summary for the user's default entity
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummary>> GetDashboardSummary()
    {
        var entityId = await ResolveEntityIdAsync();
        var summary = await _dashboardService.GetDashboardSummaryAsync(entityId);
        return Ok(summary);
    }
}
