using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Api.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.DTOs.Admin;
using CtrlValue.Application.Interfaces;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[ApiController]
[Route("api/site-admin")]
[Authorize(Policy = "SiteAdmin")]
public class SiteAdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly TenantContext _tenant;

    public SiteAdminController(IAdminService admin, TenantContext tenant)
    {
        _admin  = admin;
        _tenant = tenant;
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetTenantUsers()
    {
        var users = await _admin.GetTenantUsersAsync(_tenant.TenantId);
        return Ok(users);
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId)
    {
        await _admin.TriggerPasswordResetAsync(userId);
        return Ok(new { message = "Password reset email sent." });
    }

    // ── Entities ──────────────────────────────────────────────────────────────

    [HttpPost("entities/{entityId:guid}/invite")]
    public async Task<IActionResult> InviteUser(Guid entityId, [FromBody] InviteUserRequest request)
    {
        await _admin.InviteUserToEntityAsync(request.Email, entityId, _tenant.TenantId);
        return Ok(new { message = "Invite processed successfully." });
    }

    [HttpDelete("entities/{entityId:guid}/users/{userId:guid}")]
    public async Task<IActionResult> RemoveUserFromEntity(Guid entityId, Guid userId)
    {
        await _admin.RemoveUserFromEntityAsync(userId, entityId);
        return Ok(new { message = "User removed from entity." });
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns paginated audit log entries scoped to the caller's tenant.
    /// SuperAdmin may pass ?tenantId= to query any tenant or omit to get all.
    /// </summary>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryParams queryParams)
    {
        var isSuperAdmin = _tenant.Role == "SuperAdmin";
        var logs = await _admin.GetAuditLogsAsync(queryParams, _tenant.TenantId, isSuperAdmin);
        return Ok(logs);
    }
}
