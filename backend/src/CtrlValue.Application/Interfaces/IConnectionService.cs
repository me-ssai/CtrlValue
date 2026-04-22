using CtrlValue.Domain.Entities;

namespace CtrlValue.Application.Interfaces;

/// <summary>
/// High-level orchestrator for financial connections.
/// This is what controllers call — it delegates to IFinancialProvider via IFinancialProviderFactory.
/// All provider routing, sync logging, and connection health management happens here.
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Starts a new bank connection for the current entity.
    /// Resolves the provider from entity.Country and returns a ConnectionInitResult
    /// telling the frontend how to proceed (Plaid link token, Basiq auth URL, etc.).
    /// </summary>
    Task<ConnectionInitResult> InitiateConnectionAsync(Guid entityId, Guid userId, string? mobile = null);

    /// <summary>
    /// Completes the connection flow after the frontend returns from the external provider.
    /// Creates the FinancialConnection row, performs initial account sync, and returns the DTO.
    /// </summary>
    Task<FinancialConnectionDto> CompleteConnectionAsync(Guid entityId, string callbackPayload);

    /// <summary>Returns all FinancialConnections for the entity.</summary>
    Task<List<FinancialConnectionDto>> GetConnectionsAsync(Guid entityId);

    /// <summary>
    /// Syncs accounts and transactions for a single connection.
    /// Writes a ConnectionSyncLog entry with the outcome.
    /// </summary>
    Task<ConnectionSyncResultDto> SyncConnectionAsync(Guid connectionId, Guid entityId, DateTime? startDate = null);

    /// <summary>Revokes credentials at the provider and removes the connection + its ConnectedAccounts.</summary>
    Task RemoveConnectionAsync(Guid connectionId, Guid entityId);

    /// <summary>Returns all ConnectedAccounts for the entity across all providers.</summary>
    Task<List<ConnectedAccountDto>> GetConnectedAccountsAsync(Guid entityId);

    /// <summary>Links a ConnectedAccount to an existing Account in the system.</summary>
    Task LinkAccountAsync(Guid connectedAccountId, Guid linkedAccountId, Guid entityId);

    /// <summary>Returns the health status of every connection for the entity.</summary>
    Task<List<ConnectionHealthDto>> GetConnectionHealthAsync(Guid entityId);
}
