using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly IAccountService _accountService;

    public DashboardService(AppDbContext db, IAccountService accountService)
    {
        _db = db;
        _accountService = accountService;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(Guid entityId)
    {
        var accountSummary = await _accountService.GetAccountSummaryAsync(entityId);

        // Get transactions for current month
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var monthTransactions = await _db.Transactions
            .Where(t => t.EntityId == entityId 
                && !t.IsDeleted 
                && t.TxnTime >= monthStart 
                && t.TxnTime < monthEnd)
            .ToListAsync();

        var transactionCount = monthTransactions.Count;

        // Calculate income and expenses for this month
        decimal incomeThisMonth = monthTransactions
            .Where(t => t.TxnType == TransactionType.Income || t.TxnType == TransactionType.CapitalDeposit)
            .Sum(t => t.Amount);

        decimal expensesThisMonth = monthTransactions
            .Where(t => t.TxnType == TransactionType.Expense || t.TxnType == TransactionType.CapitalWithdrawal)
            .Sum(t => t.Amount);

        // Get recent transactions (last 10)
        var recentTransactions = await _db.Transactions
            .Where(t => t.EntityId == entityId && !t.IsDeleted)
            .Include(t => t.Category)
            .Include(t => t.Account)
            .OrderByDescending(t => t.TxnTime)
            .Take(10)
            .Select(t => new RecentTransaction
            {
                Id = t.Id,
                Date = t.TxnTime,
                Description = t.Description,
                Amount = t.Amount,
                Type = t.TxnType.ToString(),
                Category = t.Category != null ? t.Category.Name : "Uncategorized",
                AccountName = t.Account != null ? t.Account.Name : null
            })
            .ToListAsync();

        return new DashboardSummary
        {
            TotalAssets = accountSummary.TotalAssets,
            TotalLiabilities = accountSummary.TotalLiabilities,
            NetWorth = accountSummary.NetWorth,
            AssetCount = accountSummary.AssetCount,
            LiabilityCount = accountSummary.LiabilityCount,
            TransactionCountThisMonth = transactionCount,
            IncomeThisMonth = incomeThisMonth,
            ExpensesThisMonth = expensesThisMonth,
            RecentTransactions = recentTransactions,
            Holdings = accountSummary.Holdings
        };
    }
}
