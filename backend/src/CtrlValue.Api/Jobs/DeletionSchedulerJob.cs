using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Jobs;

/// <summary>
/// Runs every hour and hard-deletes any user accounts whose 30-day grace period has elapsed.
/// </summary>
public class DeletionSchedulerJob : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private readonly IServiceProvider _services;
    private readonly ILogger<DeletionSchedulerJob> _logger;

    public DeletionSchedulerJob(IServiceProvider services, ILogger<DeletionSchedulerJob> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeletionSchedulerJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletionSchedulerJob encountered an error while processing deletion requests.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ProcessDueRequestsAsync(CancellationToken ct)
    {
        // Use a scoped service provider — BackgroundService is singleton
        using var scope  = _services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletionSvc  = scope.ServiceProvider.GetRequiredService<IUserDeletionService>();

        var now = DateTime.UtcNow;

        var dueRequests = await db.DeletionRequests
            .IgnoreQueryFilters()
            .Where(r => r.Status == "Pending" && r.ScheduledDeletionAt <= now)
            .Select(r => new { r.Id, r.UserId })
            .ToListAsync(ct);

        if (dueRequests.Count == 0)
            return;

        _logger.LogInformation("DeletionSchedulerJob: found {Count} account(s) due for deletion.", dueRequests.Count);

        foreach (var req in dueRequests)
        {
            try
            {
                // Guid.Empty signals system-initiated deletion
                await deletionSvc.ExecuteUserDeletionAsync(req.UserId, Guid.Empty);
                _logger.LogInformation("DeletionSchedulerJob: deleted user {UserId}.", req.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletionSchedulerJob: failed to delete user {UserId}.", req.UserId);
            }
        }
    }
}
