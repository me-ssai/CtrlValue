using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class DepreciationScheduleService : IDepreciationScheduleService
{
    private readonly AppDbContext _db;

    public DepreciationScheduleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<DepreciationScheduleDto>> GetDepreciationSchedulesAsync(Guid entityId)
    {
        var schedules = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.Account.EntityId == entityId && !ds.IsDeleted)
            .ToListAsync();

        var result = new List<DepreciationScheduleDto>();
        foreach (var schedule in schedules)
        {
            result.Add(MapToDto(schedule));
        }

        return result;
    }

    public async Task<DepreciationScheduleDto?> GetDepreciationScheduleByIdAsync(Guid id, Guid entityId)
    {
        var schedule = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.Id == id && ds.Account.EntityId == entityId && !ds.IsDeleted)
            .FirstOrDefaultAsync();

        return schedule == null ? null : MapToDto(schedule);
    }

    public async Task<DepreciationScheduleDto?> GetDepreciationScheduleByAccountAsync(Guid accountId, Guid entityId)
    {
        var schedule = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.AccountId == accountId && ds.Account.EntityId == entityId && !ds.IsDeleted)
            .FirstOrDefaultAsync();

        return schedule == null ? null : MapToDto(schedule);
    }

    public async Task<DepreciationScheduleDto> CreateDepreciationScheduleAsync(CreateDepreciationScheduleRequest request, Guid entityId)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId && !a.IsDeleted);

        if (account == null)
            throw new KeyNotFoundException("Account not found or access denied.");

        // Check if schedule already exists for this account
        var existing = await _db.DepreciationSchedules
            .AnyAsync(ds => ds.AccountId == request.AccountId && !ds.IsDeleted);

        if (existing)
            throw new InvalidOperationException("Depreciation schedule already exists for this account.");

        var schedule = new DepreciationSchedule
        {
            AccountId = request.AccountId,
            Method = request.Method,
            PurchasePrice = request.PurchasePrice,
            PurchaseDate = request.PurchaseDate,
            UsefulLifeYears = request.UsefulLifeYears,
            SalvageValue = request.SalvageValue,
            AnnualDepreciationRate = request.AnnualDepreciationRate,
            TenantId = "default"
        };

        _db.DepreciationSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        await _db.Entry(schedule).Reference(ds => ds.Account).LoadAsync();

        return MapToDto(schedule);
    }

    public async Task<DepreciationScheduleDto> UpdateDepreciationScheduleAsync(Guid id, UpdateDepreciationScheduleRequest request, Guid entityId)
    {
        var schedule = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.Id == id && ds.Account.EntityId == entityId && !ds.IsDeleted)
            .FirstOrDefaultAsync();

        if (schedule == null)
            throw new KeyNotFoundException("Depreciation schedule not found or access denied.");

        schedule.UsefulLifeYears = request.UsefulLifeYears;
        schedule.SalvageValue = request.SalvageValue;
        schedule.AnnualDepreciationRate = request.AnnualDepreciationRate;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapToDto(schedule);
    }

    public async Task DeleteDepreciationScheduleAsync(Guid id, Guid entityId)
    {
        var schedule = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.Id == id && ds.Account.EntityId == entityId && !ds.IsDeleted)
            .FirstOrDefaultAsync();

        if (schedule == null)
            throw new KeyNotFoundException("Depreciation schedule not found or access denied.");

        schedule.IsDeleted = true;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<decimal> CalculateCurrentValueAsync(Guid id, Guid entityId, DateTime? asOfDate = null)
    {
        var schedule = await _db.DepreciationSchedules
            .Include(ds => ds.Account)
            .Where(ds => ds.Id == id && ds.Account.EntityId == entityId && !ds.IsDeleted)
            .FirstOrDefaultAsync();

        if (schedule == null)
            throw new KeyNotFoundException("Depreciation schedule not found or access denied.");

        var targetDate = asOfDate ?? DateTime.UtcNow;
        var yearsElapsed = (decimal)(targetDate - schedule.PurchaseDate).TotalDays / 365.25m;

        return schedule.Method switch
        {
            DepreciationMethod.STRAIGHT_LINE => CalculateStraightLine(schedule, yearsElapsed),
            DepreciationMethod.DECLINING_BALANCE => CalculateDecliningBalance(schedule, yearsElapsed),
            _ => schedule.PurchasePrice
        };
    }

    // Inline variant used in MapToDto to avoid a redundant DB round-trip per schedule
    private decimal CalculateCurrentValue(DepreciationSchedule schedule, DateTime? asOfDate = null)
    {
        var targetDate = asOfDate ?? DateTime.UtcNow;
        var yearsElapsed = (decimal)(targetDate - schedule.PurchaseDate).TotalDays / 365.25m;

        return schedule.Method switch
        {
            DepreciationMethod.STRAIGHT_LINE => CalculateStraightLine(schedule, yearsElapsed),
            DepreciationMethod.DECLINING_BALANCE => CalculateDecliningBalance(schedule, yearsElapsed),
            _ => schedule.PurchasePrice
        };
    }

    private decimal CalculateStraightLine(DepreciationSchedule schedule, decimal yearsElapsed)
    {
        if (!schedule.UsefulLifeYears.HasValue || schedule.UsefulLifeYears.Value == 0)
            return schedule.PurchasePrice;

        var salvageValue = schedule.SalvageValue ?? 0;
        var depreciableAmount = schedule.PurchasePrice - salvageValue;
        var annualDepreciation = depreciableAmount / schedule.UsefulLifeYears.Value;
        var totalDepreciation = Math.Min(annualDepreciation * yearsElapsed, depreciableAmount);

        return Math.Max(schedule.PurchasePrice - totalDepreciation, salvageValue);
    }

    private decimal CalculateDecliningBalance(DepreciationSchedule schedule, decimal yearsElapsed)
    {
        if (!schedule.AnnualDepreciationRate.HasValue)
            return schedule.PurchasePrice;

        var rate = schedule.AnnualDepreciationRate.Value / 100m;
        var currentValue = schedule.PurchasePrice * (decimal)Math.Pow((double)(1 - rate), (double)yearsElapsed);
        var salvageValue = schedule.SalvageValue ?? 0;

        return Math.Max(currentValue, salvageValue);
    }

    private DepreciationScheduleDto MapToDto(DepreciationSchedule schedule)
    {
        // Use inline calculation to avoid an extra DB round-trip per schedule (was N+1)
        var currentValue = CalculateCurrentValue(schedule);
        var accumulatedDepreciation = schedule.PurchasePrice - currentValue;

        return new DepreciationScheduleDto
        {
            Id = schedule.Id,
            AccountId = schedule.AccountId,
            AccountName = schedule.Account.Name,
            Method = schedule.Method.ToString(),
            PurchasePrice = schedule.PurchasePrice,
            PurchaseDate = schedule.PurchaseDate,
            UsefulLifeYears = schedule.UsefulLifeYears,
            SalvageValue = schedule.SalvageValue,
            AnnualDepreciationRate = schedule.AnnualDepreciationRate,
            CurrentValue = currentValue,
            AccumulatedDepreciation = accumulatedDepreciation,
            CreatedAt = schedule.CreatedAt
        };
    }
}

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;

    public BudgetService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<BudgetDto>> GetBudgetsAsync(Guid entityId, Guid? categoryId = null)
    {
        var query = _db.Budgets
            .Include(b => b.Category)
            .Where(b => b.EntityId == entityId && !b.IsDeleted);

        if (categoryId.HasValue)
            query = query.Where(b => b.CategoryId == categoryId.Value);

        var budgets = await query.OrderByDescending(b => b.PeriodStart).ToListAsync();

        var result = new List<BudgetDto>();
        foreach (var budget in budgets)
        {
            result.Add(await MapToDto(budget, entityId));
        }

        return result;
    }

    public async Task<BudgetDto?> GetBudgetByIdAsync(Guid id, Guid entityId)
    {
        var budget = await _db.Budgets
            .Include(b => b.Category)
            .Where(b => b.Id == id && b.EntityId == entityId && !b.IsDeleted)
            .FirstOrDefaultAsync();

        return budget == null ? null : await MapToDto(budget, entityId);
    }

    public async Task<BudgetDto> CreateBudgetAsync(CreateBudgetRequest request, Guid entityId)
    {
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.EntityId == entityId && !c.IsDeleted);

        if (category == null)
            throw new KeyNotFoundException("Category not found or access denied.");

        var budget = new Budget
        {
            EntityId = entityId,
            CategoryId = request.CategoryId,
            PeriodType = request.PeriodType,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Amount = request.Amount,
            Currency = request.Currency,
            TenantId = "default"
        };

        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        await _db.Entry(budget).Reference(b => b.Category).LoadAsync();

        return await MapToDto(budget, entityId);
    }

    public async Task<BudgetDto> UpdateBudgetAsync(Guid id, UpdateBudgetRequest request, Guid entityId)
    {
        var budget = await _db.Budgets
            .Include(b => b.Category)
            .Where(b => b.Id == id && b.EntityId == entityId && !b.IsDeleted)
            .FirstOrDefaultAsync();

        if (budget == null)
            throw new KeyNotFoundException("Budget not found or access denied.");

        budget.Amount = request.Amount;
        if (request.PeriodEnd.HasValue)
            budget.PeriodEnd = request.PeriodEnd.Value;
        budget.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await MapToDto(budget, entityId);
    }

    public async Task DeleteBudgetAsync(Guid id, Guid entityId)
    {
        var budget = await _db.Budgets
            .Where(b => b.Id == id && b.EntityId == entityId && !b.IsDeleted)
            .FirstOrDefaultAsync();

        if (budget == null)
            throw new KeyNotFoundException("Budget not found or access denied.");

        budget.IsDeleted = true;
        budget.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<BudgetDto>> GetActiveBudgetsAsync(Guid entityId, DateTime? asOfDate = null)
    {
        var targetDate = asOfDate ?? DateTime.UtcNow;

        var budgets = await _db.Budgets
            .Include(b => b.Category)
            .Where(b => b.EntityId == entityId 
                && !b.IsDeleted 
                && b.PeriodStart <= targetDate 
                && b.PeriodEnd >= targetDate)
            .ToListAsync();

        var result = new List<BudgetDto>();
        foreach (var budget in budgets)
        {
            result.Add(await MapToDto(budget, entityId));
        }

        return result;
    }

    private async Task<BudgetDto> MapToDto(Budget budget, Guid entityId)
    {
        // Calculate actual spent for this budget period
        var actualSpent = await _db.Transactions
            .Where(t => t.EntityId == entityId
                && t.CategoryId == budget.CategoryId
                && t.TxnType == TransactionType.Expense
                && t.TxnTime >= budget.PeriodStart
                && t.TxnTime <= budget.PeriodEnd
                && !t.IsDeleted)
            .SumAsync(t => t.Amount);

        var remaining = budget.Amount - actualSpent;
        var percentUsed = budget.Amount != 0 ? (actualSpent / budget.Amount) * 100 : 0;

        return new BudgetDto
        {
            Id = budget.Id,
            EntityId = budget.EntityId,
            CategoryId = budget.CategoryId,
            CategoryName = budget.Category.Name,
            PeriodType = budget.PeriodType.ToString(),
            PeriodStart = budget.PeriodStart,
            PeriodEnd = budget.PeriodEnd,
            Amount = budget.Amount,
            Currency = budget.Currency,
            ActualSpent = actualSpent,
            Remaining = remaining,
            PercentUsed = percentUsed,
            CreatedAt = budget.CreatedAt
        };
    }
}
