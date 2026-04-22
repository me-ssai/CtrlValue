using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentSavingsHistoryService : IAgentSavingsHistoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AgentSavingsHistoryService> _logger;

    public AgentSavingsHistoryService(AppDbContext db, ILogger<AgentSavingsHistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<SavingsSnapshotDto>> GetHistoryAsync(Guid entityId, int months = 24)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);

        return await _db.AgentSavingsSnapshots
            .AsNoTracking()
            .Where(s => s.EntityId == entityId && s.AsOfDate >= cutoff)
            .OrderBy(s => s.AsOfDate)
            .Select(s => new SavingsSnapshotDto
            {
                Id = s.Id,
                AsOfDate = s.AsOfDate,
                SavingsRatePercent = s.SavingsRatePercent,
                AverageMonthlyIncome = s.AverageMonthlyIncome,
                AverageMonthlyExpenses = s.AverageMonthlyExpenses,
                AverageMonthlySavings = s.AverageMonthlySavings,
                Currency = s.Currency
            })
            .ToListAsync();
    }

    public async Task RecordSnapshotAsync(Guid userId, Guid entityId, FinanceContextDto ctx, CancellationToken ct = default)
    {
        try
        {
            // Normalise to first day of current month
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var existing = await _db.AgentSavingsSnapshots
                .FirstOrDefaultAsync(s => s.EntityId == entityId && s.AsOfDate == monthStart, ct);

            if (existing != null)
            {
                // Update if the context data has changed
                existing.SavingsRatePercent     = ctx.Saving.SavingsRatePercent;
                existing.AverageMonthlyIncome   = ctx.Saving.AverageMonthlyIncome;
                existing.AverageMonthlyExpenses = ctx.Saving.AverageMonthlyExpenses;
                existing.AverageMonthlySavings  = ctx.Saving.AverageMonthlySavings;
                existing.Currency               = ctx.Currency;
                existing.UpdatedAt              = now;
            }
            else
            {
                _db.AgentSavingsSnapshots.Add(new AgentSavingsSnapshot
                {
                    UserId                  = userId,
                    EntityId                = entityId,
                    AsOfDate                = monthStart,
                    SavingsRatePercent      = ctx.Saving.SavingsRatePercent,
                    AverageMonthlyIncome    = ctx.Saving.AverageMonthlyIncome,
                    AverageMonthlyExpenses  = ctx.Saving.AverageMonthlyExpenses,
                    AverageMonthlySavings   = ctx.Saving.AverageMonthlySavings,
                    Currency                = ctx.Currency,
                    TenantId                = ""
                });
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Non-fatal — don't abort context builds if snapshot recording fails
            _logger.LogWarning(ex,
                "[SavingsHistory] Failed to record snapshot for entity {EntityId}", entityId);
        }
    }
}
