using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Jobs;

/// <summary>
/// Background job that runs every 24 hours and refreshes agent insights for all active entities.
///
/// Proactive intelligence pipeline:
///   1. Find all entities that have at least one user with AgentCore + AlertsNudges section enabled
///   2. Call RefreshInsightsAsync for each — this runs rule-based detectors and upserts insights
///   3. Errors are logged per-entity and do not abort the run for other entities
///
/// Follow-on: weekly digest emails and push notifications can be layered on top of this job (Phase 5+).
/// </summary>
public class AgentAlertJob : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<AgentAlertJob> _logger;

    public AgentAlertJob(IServiceProvider services, ILogger<AgentAlertJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentAlertJob started.");

        // Stagger startup so it doesn't compete with PriceFetchJob
        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AgentAlertJob encountered an unexpected error.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flags       = scope.ServiceProvider.GetRequiredService<IAgentFeatureFlagService>();
        var insights    = scope.ServiceProvider.GetRequiredService<IAgentInsightService>();

        _logger.LogInformation("AgentAlertJob: starting insight refresh run at {Time:u}", DateTime.UtcNow);

        // Find all distinct entity+user pairs with active entity users
        var entityUsers = await db.EntityUsers
            .AsNoTracking()
            .Where(eu => !eu.IsDeleted)
            .Select(eu => new { eu.EntityId, eu.UserId })
            .ToListAsync(ct);

        int processed = 0, skipped = 0, errored = 0;

        foreach (var eu in entityUsers)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Only refresh if both AgentCore and AlertsNudges are accessible for this user
                var coreEnabled   = await flags.IsSectionAccessibleAsync(eu.UserId, Domain.Enums.AgentSectionKey.AgentCore);
                var alertsEnabled = await flags.IsSectionAccessibleAsync(eu.UserId, Domain.Enums.AgentSectionKey.AlertsNudges);

                if (!coreEnabled || !alertsEnabled)
                {
                    skipped++;
                    continue;
                }

                await insights.RefreshInsightsAsync(eu.UserId, eu.EntityId, ct);
                processed++;

                _logger.LogDebug(
                    "AgentAlertJob: refreshed insights for entity {EntityId} / user {UserId}",
                    eu.EntityId, eu.UserId);
            }
            catch (Exception ex)
            {
                errored++;
                _logger.LogError(ex,
                    "AgentAlertJob: failed to refresh insights for entity {EntityId} / user {UserId}",
                    eu.EntityId, eu.UserId);
            }
        }

        _logger.LogInformation(
            "AgentAlertJob: run complete — {Processed} processed, {Skipped} skipped (flags off), {Errored} errors.",
            processed, skipped, errored);
    }
}
