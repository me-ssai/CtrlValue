using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Api.Controllers;

/// <summary>
/// Unified financial connections API.
/// Replaces the old api/plaid/* and api/basiq/* endpoints.
/// The frontend never needs to know which provider is being used.
/// </summary>
[ApiController]
[Route("api/connections")]
[Authorize]
public class ConnectionsController : EntityContextController
{
    private readonly IConnectionService _connectionService;

    public ConnectionsController(
        IConnectionService connectionService,
        IEntityService entityService,
        IPermissionService permissions)
        : base(entityService, permissions)
    {
        _connectionService = connectionService;
    }

    /// <summary>
    /// Initiates a bank connection for the current entity.
    /// Returns { type: "link_token" | "auth_url" | "none", value: "..." }
    /// The frontend uses 'type' to determine how to proceed:
    ///   - link_token → open Plaid Link widget
    ///   - auth_url   → open popup to that URL (Basiq consent)
    ///   - none       → no external step needed (Manual/CSV)
    /// </summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateConnection([FromBody] InitiateConnectionRequest? request = null)
    {
        var entityId = await ResolveEntityIdAsync();
        var userId   = GetUserId();

        var result = await _connectionService.InitiateConnectionAsync(entityId, userId, request?.Mobile);
        return Ok(new { type = result.Type, value = result.Value });
    }

    /// <summary>
    /// Completes the connection flow after the user returns from the external provider.
    /// For Plaid: payload is the public_token from Link onSuccess.
    /// For Basiq: payload is empty (we poll Basiq).
    /// For CSV:   payload is JSON { "institutionName": "...", "country": "AU" }.
    /// For Manual: payload is empty.
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> CompleteConnection([FromBody] CompleteConnectionRequest request)
    {
        var entityId = await ResolveEntityIdAsync();

        var dto = await _connectionService.CompleteConnectionAsync(entityId, request.Payload ?? string.Empty);
        return Ok(dto);
    }

    /// <summary>Returns all connections for the current entity across all providers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetConnections()
    {
        var entityId = await ResolveEntityIdAsync();

        var connections = await _connectionService.GetConnectionsAsync(entityId);
        return Ok(connections);
    }

    /// <summary>Triggers an immediate sync for a specific connection.</summary>
    [HttpPost("{connectionId:guid}/sync")]
    public async Task<IActionResult> SyncConnection(Guid connectionId, [FromBody] SyncConnectionRequest? request)
    {
        var entityId = await ResolveEntityIdAsync();

        var result = await _connectionService.SyncConnectionAsync(
            connectionId, entityId, request?.StartDate);
        return Ok(result);
    }

    /// <summary>Removes a connection and revokes credentials at the provider.</summary>
    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> RemoveConnection(Guid connectionId)
    {
        var entityId = await ResolveEntityIdAsync();

        await _connectionService.RemoveConnectionAsync(connectionId, entityId);
        return NoContent();
    }

    /// <summary>Returns all connected accounts across all connections for this entity.</summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> GetConnectedAccounts()
    {
        var entityId = await ResolveEntityIdAsync();

        var accounts = await _connectionService.GetConnectedAccountsAsync(entityId);
        return Ok(accounts);
    }

    /// <summary>Links a ConnectedAccount to an Account in the system for balance sync.</summary>
    [HttpPut("accounts/{connectedAccountId:guid}/link")]
    public async Task<IActionResult> LinkAccount(Guid connectedAccountId, [FromBody] LinkAccountRequest request)
    {
        var entityId = await ResolveEntityIdAsync();

        await _connectionService.LinkAccountAsync(connectedAccountId, request.LinkedAccountId, entityId);
        return NoContent();
    }

    /// <summary>Returns the health status of every connection for this entity.</summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetConnectionHealth()
    {
        var entityId = await ResolveEntityIdAsync();

        var health = await _connectionService.GetConnectionHealthAsync(entityId);
        return Ok(health);
    }
}

public record InitiateConnectionRequest(string? Mobile);
public record CompleteConnectionRequest(string? Payload);
public record SyncConnectionRequest(DateTime? StartDate);
public record LinkAccountRequest(Guid LinkedAccountId);
