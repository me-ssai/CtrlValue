using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Orchestrates all financial connection operations.
/// Delegates to the correct IFinancialProvider via IFinancialProviderFactory.
/// Manages FinancialConnection entities, ConnectedAccount entities, and ConnectionSyncLog entries.
/// This is the single entry point for all connection operations from the API layer.
/// </summary>
public class ConnectionService : IConnectionService
{
    private readonly AppDbContext _db;
    private readonly IFinancialProviderFactory _factory;
    private readonly IEntityService _entityService;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        AppDbContext db,
        IFinancialProviderFactory factory,
        IEntityService entityService,
        ILogger<ConnectionService> logger)
    {
        _db            = db;
        _factory       = factory;
        _entityService = entityService;
        _logger        = logger;
    }

    // ── Initiate Connection ──────────────────────────────────────────────────

    public async Task<ConnectionInitResult> InitiateConnectionAsync(Guid entityId, Guid userId, string? mobile = null)
    {
        var entity = await _db.Entities.FindAsync(entityId)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found.");

        var provider = _factory.Resolve(entity.Country);
        return await provider.InitiateConnectionAsync(entityId, userId, entity.TenantId, mobile);
    }

    // ── Complete Connection ──────────────────────────────────────────────────

    public async Task<FinancialConnectionDto> CompleteConnectionAsync(Guid entityId, string callbackPayload)
    {
        var entity = await _db.Entities.FindAsync(entityId)
            ?? throw new KeyNotFoundException($"Entity {entityId} not found.");

        var provider = _factory.Resolve(entity.Country);
        return await provider.CompleteConnectionAsync(entityId, entity.TenantId, callbackPayload);
    }

    // ── Get Connections ──────────────────────────────────────────────────────

    public async Task<List<FinancialConnectionDto>> GetConnectionsAsync(Guid entityId)
    {
        var connections = await _db.FinancialConnections
            .Where(c => c.EntityId == entityId)
            .ToListAsync();

        var result = new List<FinancialConnectionDto>();
        foreach (var conn in connections)
        {
            var count = await _db.ConnectedAccounts.CountAsync(a => a.ConnectionId == conn.Id);
            result.Add(ToConnectionDto(conn, count));
        }
        return result;
    }

    // ── Sync Connection ──────────────────────────────────────────────────────

    public async Task<ConnectionSyncResultDto> SyncConnectionAsync(
        Guid connectionId, Guid entityId, DateTime? startDate = null)
    {
        var connection = await _db.FinancialConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.EntityId == entityId)
            ?? throw new KeyNotFoundException($"FinancialConnection {connectionId} not found.");

        var provider = _factory.Resolve(connection.Provider);
        var started  = DateTime.UtcNow;

        connection.LastSyncAttemptedAt = started;
        await _db.SaveChangesAsync();

        try
        {
            await provider.SyncAccountsAsync(connection);

            var endDate   = DateTime.UtcNow;
            var fromDate  = startDate ?? endDate.AddDays(-30);
            var staged    = await provider.SyncTransactionsAsync(connection, fromDate, endDate);

            connection.Status       = ConnectionStatus.Active;
            connection.StatusMessage = null;
            connection.LastSyncedAt = DateTime.UtcNow;

            var syncLog = new ConnectionSyncLog
            {
                ConnectionId       = connectionId,
                TenantId           = connection.TenantId,
                Status             = "Success",
                AccountsSynced     = true ? 1 : 0,
                TransactionsStaged = staged,
                Duration           = DateTime.UtcNow - started
            };
            _db.ConnectionSyncLogs.Add(syncLog);
            await _db.SaveChangesAsync();

            return new ConnectionSyncResultDto(true, staged, "Success", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for connection {ConnectionId}", connectionId);

            // After 3 consecutive failures, mark as NeedsReauth; otherwise Error
            var recentFailures = await _db.ConnectionSyncLogs
                .Where(l => l.ConnectionId == connectionId && l.Status == "Failed")
                .OrderByDescending(l => l.CreatedAt)
                .Take(3)
                .CountAsync();

            connection.Status       = recentFailures >= 2
                ? ConnectionStatus.NeedsReauth
                : ConnectionStatus.Error;
            connection.StatusMessage = ex.Message;

            var syncLog = new ConnectionSyncLog
            {
                ConnectionId   = connectionId,
                TenantId       = connection.TenantId,
                Status         = "Failed",
                ErrorMessage   = ex.Message,
                Duration       = DateTime.UtcNow - started
            };
            _db.ConnectionSyncLogs.Add(syncLog);
            await _db.SaveChangesAsync();

            return new ConnectionSyncResultDto(false, 0, "Failed", ex.Message);
        }
    }

    // ── Remove Connection ────────────────────────────────────────────────────

    public async Task RemoveConnectionAsync(Guid connectionId, Guid entityId)
    {
        var connection = await _db.FinancialConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.EntityId == entityId)
            ?? throw new KeyNotFoundException($"FinancialConnection {connectionId} not found.");

        var provider = _factory.Resolve(connection.Provider);
        await provider.RevokeConnectionAsync(connection);

        // Soft-deletes the connection; EF cascade soft-deletes ConnectedAccounts
        _db.FinancialConnections.Remove(connection);
        await _db.SaveChangesAsync();
    }

    // ── Get Connected Accounts ───────────────────────────────────────────────

    public async Task<List<ConnectedAccountDto>> GetConnectedAccountsAsync(Guid entityId)
    {
        var accounts = await _db.ConnectedAccounts
            .Where(a => a.EntityId == entityId)
            .ToListAsync();

        return accounts.Select(ToAccountDto).ToList();
    }

    // ── Link Account ─────────────────────────────────────────────────────────

    public async Task LinkAccountAsync(Guid connectedAccountId, Guid linkedAccountId, Guid entityId)
    {
        var connectedAccount = await _db.ConnectedAccounts
            .FirstOrDefaultAsync(a => a.Id == connectedAccountId && a.EntityId == entityId)
            ?? throw new KeyNotFoundException($"ConnectedAccount {connectedAccountId} not found.");

        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.EntityId == entityId)
            ?? throw new KeyNotFoundException($"Account {linkedAccountId} not found.");

        var connection = await _db.FinancialConnections.FindAsync(connectedAccount.ConnectionId);

        connectedAccount.LinkedAccountId = linkedAccountId;
        account.IsSyncEnabled       = true;
        account.ConnectionProvider  = connection?.Provider;
        account.LastSyncedAt        = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ── Connection Health ────────────────────────────────────────────────────

    public async Task<List<ConnectionHealthDto>> GetConnectionHealthAsync(Guid entityId)
    {
        var connections = await _db.FinancialConnections
            .Where(c => c.EntityId == entityId)
            .ToListAsync();

        return connections.Select(c => new ConnectionHealthDto(
            c.Id,
            c.InstitutionName,
            c.Provider.ToString(),
            MapHealthStatus(c.Status),
            c.LastSyncedAt,
            c.ConsentExpiresAt,
            c.StatusMessage
        )).ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string MapHealthStatus(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Active       => "Healthy",
        ConnectionStatus.NeedsReauth  => "NeedsReauth",
        ConnectionStatus.Error        => "Error",
        ConnectionStatus.Expired      => "Expired",
        ConnectionStatus.Disconnected => "Error",
        _                             => "Unknown"
    };

    private static FinancialConnectionDto ToConnectionDto(FinancialConnection c, int accountCount) => new(
        c.Id, c.EntityId, c.Provider.ToString(), c.ProviderConnectionId,
        c.InstitutionName, c.InstitutionLogoUrl, c.Status.ToString(), c.StatusMessage,
        c.Country, c.LastSyncedAt, c.ConsentExpiresAt, accountCount);

    private static ConnectedAccountDto ToAccountDto(ConnectedAccount a) => new(
        a.Id, a.ConnectionId, a.ExternalAccountId, a.Name, a.OfficialName,
        a.Mask, a.Type, a.Subtype, a.CurrentBalance, a.AvailableBalance,
        a.CurrencyCode, a.IsActive, a.LinkedAccountId, a.LastSyncedAt);
}
