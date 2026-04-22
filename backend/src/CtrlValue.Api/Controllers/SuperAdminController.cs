using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.DTOs.Admin;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using CtrlValue.Api.Jobs;

namespace CtrlValue.Api.Controllers;

[ApiController]
[Route("api/super-admin")]
[Authorize(Policy = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IWebHostEnvironment _env;
    private readonly IUserDeletionService _userDeletion;
    private readonly PriceFetchJob _priceFetchJob;

    public SuperAdminController(IAdminService admin, IWebHostEnvironment env, IUserDeletionService userDeletion, PriceFetchJob priceFetchJob)
    {
        _admin         = admin;
        _env           = env;
        _userDeletion  = userDeletion;
        _priceFetchJob = priceFetchJob;
    }

    // ── Price Fetch ───────────────────────────────────────────────────────────

    [HttpPost("price-fetch/trigger")]
    public async Task<IActionResult> TriggerPriceFetch()
    {
        await _priceFetchJob.RunAsync(CancellationToken.None);
        return Ok(new { message = "Price fetch completed." });
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _admin.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = "Invalid role value. Valid values: SuperAdmin, SiteAdmin, User." });

        await _admin.UpdateUserRoleAsync(userId, role);
        return Ok(new { message = "User role updated." });
    }

    [HttpPost("users/{userId:guid}/impersonate")]
    public async Task<IActionResult> ImpersonateUser(Guid userId)
    {
        var actingAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var token = await _admin.ImpersonateUserAsync(actingAdminId, userId);

        // Set impersonation token as httpOnly cookie (15-min expiry matches token lifetime)
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure   = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires  = DateTime.UtcNow.AddMinutes(15),
            Path     = "/"
        });

        return Ok(new { message = "Impersonation session started." });
    }

    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId)
    {
        await _admin.TriggerPasswordResetAsync(userId);
        return Ok(new { message = "Password reset email sent." });
    }

    [HttpDelete("users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var actingAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _userDeletion.ExecuteUserDeletionAsync(userId, actingAdminId);
        return Ok(new { message = "User and all associated data permanently deleted." });
    }

    // ── Tenants ───────────────────────────────────────────────────────────────

    [HttpGet("tenants")]
    public async Task<IActionResult> GetAllTenants()
    {
        var tenants = await _admin.GetAllTenantsAsync();
        return Ok(tenants);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var tenant = await _admin.CreateTenantAsync(request);
        return CreatedAtAction(nameof(GetAllTenants), new { }, tenant);
    }

    [HttpPatch("tenants/{tenantId:guid}/active")]
    public async Task<IActionResult> SetTenantActive(Guid tenantId, [FromQuery] bool isActive)
    {
        await _admin.SetTenantActiveAsync(tenantId, isActive);
        return Ok(new { message = $"Tenant {(isActive ? "activated" : "deactivated")}." });
    }

    [HttpDelete("tenants/{tenantId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(Guid tenantId)
    {
        var actingAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _admin.DeleteTenantAsync(tenantId, actingAdminId);
        return Ok(new { message = "Tenant and all associated users and data permanently deleted." });
    }

    // ── Deletion Requests ─────────────────────────────────────────────────────

    [HttpGet("deletion-requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeletionRequests()
    {
        var requests = await _admin.GetDeletionRequestsAsync();
        return Ok(requests);
    }

    [HttpPost("deletion-requests/{requestId:guid}/approve-expedite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveExpeditedDeletion(Guid requestId)
    {
        var actingUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _admin.ApproveExpeditedDeletionAsync(requestId, actingUserId);
        return Ok(new { message = "Deletion approved and executed." });
    }

    [HttpPost("deletion-requests/{requestId:guid}/reject-expedite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectExpeditedDeletion(Guid requestId, [FromBody] RejectDeletionRequest body)
    {
        var actingUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _admin.RejectExpeditedDeletionAsync(requestId, actingUserId, body.Reason);
        return Ok(new { message = "Expedited deletion request rejected. The 30-day schedule resumes." });
    }

    [HttpPut("users/{userId:guid}/deletion-approver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDeletionApprover(Guid userId, [FromBody] SetDeletionApproverRequest body)
    {
        var actingAdminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _admin.SetDeletionApproverAsync(userId, body.CanApprove, actingAdminId);
        return Ok(new { message = $"User deletion-approver privilege {(body.CanApprove ? "granted" : "revoked")}." });
    }
}
