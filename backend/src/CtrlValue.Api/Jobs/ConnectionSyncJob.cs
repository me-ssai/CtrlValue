using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Jobs;

/// <summary>
/// Background job that periodically syncs all active FinancialConnections.
/// Runs on IHostedService (consistent with PriceFetchJob and DeletionSchedulerJob).
///
/// Schedule:
///   Every 15 minutes — checks which connections are due for a sync.
///   Active connections sync every 4 hours.
///   Connections with errors are retried every 30 minutes.
///   NeedsReauth connections are skipped (user action required).
/// </summary>
public class ConnectionSyncJob : BackgroundService
{
    private static readonly TimeSpan CheckInterval   = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ActiveSyncAge   = TimeSpan.FromHours(4);
    private static readonly TimeSpan ErrorRetryAge   = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<ConnectionSyncJob> _logger;

    public ConnectionSyncJob(IServiceProvider services, ILogger<ConnectionSyncJob> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConnectionSyncJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSyncCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectionSyncJob cycle failed.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db              = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionService = scope.ServiceProvider.GetRequiredService<IConnectionService>();

        var now = DateTime.UtcNow;

        // Query connections that need syncing
        var connections = await db.FinancialConnections
            .Where(c => c.Status != ConnectionStatus.NeedsReauth
                     && c.Status != ConnectionStatus.Disconnected
                     && c.Provider != FinancialConnectionProvider.Manual
                     && c.Provider != FinancialConnectionProvider.Csv)
            .ToListAsync(ct);

        var due = connections.Where(c =>
        {
            if (c.Status == ConnectionStatus.Error || c.Status == ConnectionStatus.Expired)
                return c.LastSyncAttemptedAt == null || now - c.LastSyncAttemptedAt > ErrorRetryAge;

            // Active — sync every 4 hours
            return c.LastSyncedAt == null || now - c.LastSyncedAt > ActiveSyncAge;
        }).ToList();

        if (!due.Any())
        {
            _logger.LogDebug("ConnectionSyncJob: no connections due for sync.");
            return;
        }

        _logger.LogInformation("ConnectionSyncJob: syncing {Count} connection(s).", due.Count);

        foreach (var connection in due)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await connectionService.SyncConnectionAsync(
                    connection.Id, connection.EntityId,
                    startDate: null);

                _logger.LogInformation("Auto-synced connection {Id} ({Institution})",
                    connection.Id, connection.InstitutionName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-sync failed for connection {Id} ({Institution})",
                    connection.Id, connection.InstitutionName);
            }
        }
    }
}
