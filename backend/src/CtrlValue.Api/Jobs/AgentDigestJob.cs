using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Jobs;

/// <summary>
/// Weekly background job that generates digest emails for all entities with
/// AgentCore + AlertsNudges enabled. Digests are saved as "Pending" and require
/// admin approval before being sent (toggleable via AgentSetting "RequireDigestApproval").
/// </summary>
public class AgentDigestJob : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(168); // 7 days
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(30); // stagger after boot

    private readonly IServiceProvider _services;
    private readonly ILogger<AgentDigestJob> _logger;

    public AgentDigestJob(IServiceProvider services, ILogger<AgentDigestJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AgentDigestJob] Starting — first run in {Delay}", StartupDelay);
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[AgentDigestJob] Unhandled error during run");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("[AgentDigestJob] Starting weekly digest generation run");

        using var scope = _services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var flags   = scope.ServiceProvider.GetRequiredService<IAgentFeatureFlagService>();
        var digest  = scope.ServiceProvider.GetRequiredService<IAgentDigestService>();

        // Find all entity-user pairs with AgentCore + AlertsNudges accessible
        var entityUsers = await db.EntityUsers
            .AsNoTracking()
            .Where(eu => !eu.IsDeleted)
            .Select(eu => new { eu.UserId, eu.EntityId })
            .Distinct()
            .ToListAsync(ct);

        int generated = 0;
        int skipped   = 0;

        foreach (var eu in entityUsers)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var coreOk   = await flags.IsSectionAccessibleAsync(eu.UserId, AgentSectionKey.AgentCore);
                var alertsOk = await flags.IsSectionAccessibleAsync(eu.UserId, AgentSectionKey.AlertsNudges);

                if (!coreOk || !alertsOk)
                {
                    skipped++;
                    continue;
                }

                await digest.GenerateDigestAsync(eu.UserId, eu.EntityId, ct);
                generated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[AgentDigestJob] Failed to generate digest for entity {EntityId}", eu.EntityId);
            }
        }

        _logger.LogInformation(
            "[AgentDigestJob] Run complete. Generated: {Generated}, Skipped (flags): {Skipped}",
            generated, skipped);
    }
}
