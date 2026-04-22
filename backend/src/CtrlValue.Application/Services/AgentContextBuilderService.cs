using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentContextBuilderService : IAgentContextBuilderService
{
    private readonly AppDbContext _db;
    private readonly IAccountService _accountService;
    private readonly ITransactionIntelligenceService _intelligence;
    private readonly IAgentSavingsHistoryService _savingsHistory;
    private readonly ILogger<AgentContextBuilderService> _logger;

    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(30);

    // Essential spend category keywords (lowercase)
    private static readonly HashSet<string> EssentialKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "rent", "mortgage", "utilities", "electricity", "gas", "water", "internet",
        "insurance", "groceries", "supermarket", "pharmacy", "medical", "health",
        "transport", "fuel", "petrol", "rates", "council", "school", "childcare"
    };

    public AgentContextBuilderService(
        AppDbContext db,
        IAccountService accountService,
        ITransactionIntelligenceService intelligence,
        IAgentSavingsHistoryService savingsHistory,
        ILogger<AgentContextBuilderService> logger)
    {
        _db = db;
        _accountService = accountService;
        _intelligence = intelligence;
        _savingsHistory = savingsHistory;
        _logger = logger;
    }

    public async Task<FinanceContextDto> BuildContextAsync(
        Guid userId,
        Guid entityId,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!forceRefresh)
        {
            var cached = await _db.AgentContextSnapshots
                .Where(s => s.EntityId == entityId && s.SnapshotType == "full" && s.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (cached != null)
            {
                _logger.LogDebug("[AgentContext] Cache hit for entity {EntityId}", entityId);
                var cachedCtx = JsonSerializer.Deserialize<FinanceContextDto>(cached.Payload);
                if (cachedCtx != null) return cachedCtx;
            }
        }

        _logger.LogInformation("[AgentContext] Building fresh context for entity {EntityId}", entityId);

        var context = await BuildFreshAsync(entityId, ct);

        await PersistSnapshotAsync(userId, entityId, context, ct);

        // Record monthly savings snapshot (non-fatal, fire-and-forget if it fails)
        await _savingsHistory.RecordSnapshotAsync(userId, entityId, context, ct);

        return context;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Fresh build
    // ────────────────────────────────────────────────────────────────────────────

    private async Task<FinanceContextDto> BuildFreshAsync(Guid entityId, CancellationToken ct)
    {
        // Sequential — all services share the same scoped DbContext instance.
        // EF Core does not support concurrent operations on a single context.
        var summary      = await _accountService.GetAccountSummaryAsync(entityId);
        var cashFlow     = await _intelligence.GetCashFlowAsync(entityId, months: 6);
        var spendingTrend = await _intelligence.GetSpendingTrendAsync(entityId, months: 3);
        var recurring    = await _intelligence.DetectRecurringPatternsAsync(entityId, lookbackMonths: 6);
        var loanDetails  = await _db.LoanDetails
            .AsNoTracking()
            .Where(l => l.Account.EntityId == entityId)
            .Include(l => l.Account)
            .ToListAsync(ct);

        // ── Net Worth ───────────────────────────────────────────────────────────
        var byAssetClass = summary.Holdings
            .Where(h => h.AssetClass != null)
            .GroupBy(h => h.AssetClass!)
            .ToDictionary(g => g.Key, g => g.Sum(h => h.Value));

        var netWorth = new NetWorthSummaryDto
        {
            Total = summary.NetWorth,
            TotalAssets = summary.TotalAssets,
            TotalLiabilities = summary.TotalLiabilities,
            ByAssetClass = byAssetClass
        };

        // ── Cash Flow averages ──────────────────────────────────────────────────
        decimal avgMonthlyIncome = cashFlow.Count > 0
            ? cashFlow.Average(m => m.TotalIncome) : 0;
        decimal avgMonthlyExpenses = cashFlow.Count > 0
            ? cashFlow.Average(m => m.TotalExpenses) : 0;
        decimal avgMonthlySavings = avgMonthlyIncome - avgMonthlyExpenses;
        decimal savingsRate = avgMonthlyIncome > 0
            ? Math.Round(avgMonthlySavings / avgMonthlyIncome * 100, 1) : 0;

        // ── Cash Position ───────────────────────────────────────────────────────
        var cashHoldings = summary.Holdings
            .Where(h => h.AssetClass == AssetClass.CASH.ToString())
            .ToList();

        decimal totalCash = cashHoldings.Sum(h => h.Value);
        decimal emergencyFundEstimate = avgMonthlyExpenses * 3;
        decimal idleCash = Math.Max(0, totalCash - emergencyFundEstimate);
        decimal monthlySurplus = cashFlow.Any()
            ? cashFlow.Last().Net : 0;

        var cash = new CashPositionDto
        {
            TotalCashBalance = totalCash,
            EstimatedIdleCash = idleCash,
            EmergencyFundEstimate = emergencyFundEstimate,
            MonthlyCashSurplusDeficit = monthlySurplus,
            CashAccounts = cashHoldings.Select(h => new ContextAccountItemDto
            {
                Name = h.AccountName,
                Institution = h.Institution,
                Balance = h.Value,
                AssetClass = h.AssetClass ?? "CASH"
            }).ToList()
        };

        // ── Spending Behaviour ──────────────────────────────────────────────────
        var categoryTotals = spendingTrend
            .GroupBy(s => s.CategoryName)
            .Select(g => new
            {
                CategoryName = g.Key,
                MonthlyAverage = g.Average(s => s.Total),
                Last30Days = g.Where(s =>
                    s.Year == DateTime.UtcNow.Year && s.Month == DateTime.UtcNow.Month)
                    .Sum(s => s.Total)
            })
            .OrderByDescending(c => c.MonthlyAverage)
            .ToList();

        decimal totalMonthlySpend = categoryTotals.Sum(c => c.MonthlyAverage);

        var topCategories = categoryTotals
            .Take(8)
            .Select(c => new CategorySpendDto
            {
                CategoryName = c.CategoryName,
                MonthlyAverage = Math.Round(c.MonthlyAverage, 2),
                Last30Days = Math.Round(c.Last30Days, 2),
                PercentOfTotal = totalMonthlySpend > 0
                    ? Math.Round(c.MonthlyAverage / totalMonthlySpend * 100, 1) : 0
            }).ToList();

        decimal essentialEstimate = categoryTotals
            .Where(c => EssentialKeywords.Any(kw => c.CategoryName.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .Sum(c => c.MonthlyAverage);

        // Trend: compare last month vs 3 months ago
        string trendDirection = "Stable";
        if (cashFlow.Count >= 3)
        {
            var lastMonth = cashFlow.Last().TotalExpenses;
            var threeMonthsAgo = cashFlow[^3].TotalExpenses;
            if (lastMonth > threeMonthsAgo * 1.1m) trendDirection = "Up";
            else if (lastMonth < threeMonthsAgo * 0.9m) trendDirection = "Down";
        }

        var subscriptions = recurring
            .Where(r => r.Cadence is "Monthly" or "Weekly" or "Fortnightly" or "Annual")
            .Select(r => new SubscriptionDto
            {
                MerchantName = r.DisplayName ?? r.MerchantNormalised,
                TypicalAmount = r.TypicalAmount,
                Cadence = r.Cadence
            }).ToList();

        // Normalise subscription total to monthly
        decimal subMonthlyTotal = subscriptions.Sum(s => s.Cadence switch
        {
            "Weekly" => s.TypicalAmount * 4.33m,
            "Fortnightly" => s.TypicalAmount * 2.17m,
            "Annual" => s.TypicalAmount / 12m,
            _ => s.TypicalAmount
        });

        // LifestyleCreep: compare recent 3 months vs prior 3 months spending
        decimal? spendingGrowthPercent = null;
        if (cashFlow.Count >= 6)
        {
            var recent3Avg  = cashFlow.TakeLast(3).Average(m => m.TotalExpenses);
            var prior3Avg   = cashFlow.Take(3).Average(m => m.TotalExpenses);
            if (prior3Avg > 0)
                spendingGrowthPercent = Math.Round((recent3Avg - prior3Avg) / prior3Avg * 100, 1);
        }

        var spending = new SpendingBehaviourDto
        {
            MonthlyAverageTotal = Math.Round(totalMonthlySpend, 2),
            EssentialEstimate = Math.Round(essentialEstimate, 2),
            DiscretionaryEstimate = Math.Round(totalMonthlySpend - essentialEstimate, 2),
            TopCategories = topCategories,
            Subscriptions = subscriptions,
            MonthlySubscriptionTotal = Math.Round(subMonthlyTotal, 2),
            TrendDirection = trendDirection,
            SpendingGrowthPercent = spendingGrowthPercent
        };

        // ── Saving Behaviour ────────────────────────────────────────────────────
        string consistency = "Unknown";
        if (cashFlow.Count >= 3)
        {
            int positiveMonths = cashFlow.Count(m => m.Net > 0);
            double ratio = (double)positiveMonths / cashFlow.Count;
            consistency = ratio switch
            {
                >= 0.8 => "Consistent",
                >= 0.5 => "Irregular",
                _ => savingsRate < 0 ? "Negative" : "Declining"
            };
        }

        var saving = new SavingBehaviourDto
        {
            AverageMonthlyIncome = Math.Round(avgMonthlyIncome, 2),
            AverageMonthlyExpenses = Math.Round(avgMonthlyExpenses, 2),
            AverageMonthlySavings = Math.Round(avgMonthlySavings, 2),
            SavingsRatePercent = savingsRate,
            Consistency = consistency,
            MonthsAnalysed = cashFlow.Count
        };

        // ── Liability Position ──────────────────────────────────────────────────
        var liabilityHoldings = summary.Holdings
            .Where(h => h.Value < 0)  // Liabilities have negative value in the summary
            .ToList();

        // Match loan details to liability accounts for repayment info
        var loanByAccount = loanDetails.ToDictionary(l => l.AccountId, l => l);

        var liabilityItems = summary.Holdings
            .Where(h => h.Value < 0)
            .Select(h =>
            {
                loanByAccount.TryGetValue(h.AccountId, out var loan);
                return new ContextLiabilityItemDto
                {
                    Name = h.AccountName,
                    Institution = h.Institution,
                    Balance = Math.Abs(h.Value),
                    MonthlyRepayment = loan?.RepaymentAmount,
                    InterestRate = loan != null ? (decimal?)loan.InterestRate : null,
                    HasLoanDetails = loan != null
                };
            }).ToList();

        decimal totalMonthlyRepayments = liabilityItems
            .Where(l => l.MonthlyRepayment.HasValue)
            .Sum(l => l.MonthlyRepayment!.Value);

        decimal debtToIncomeRatio = avgMonthlyIncome > 0
            ? Math.Round(totalMonthlyRepayments / avgMonthlyIncome, 2) : 0;

        var liabilities = new LiabilityPositionDto
        {
            TotalDebt = summary.TotalLiabilities,
            TotalMonthlyRepayments = Math.Round(totalMonthlyRepayments, 2),
            DebtToIncomeRatio = debtToIncomeRatio,
            Liabilities = liabilityItems
        };

        // ── Investment Position ─────────────────────────────────────────────────
        var investmentClasses = new[] { "STOCK", "ETF", "METAL", "CRYPTO", "SUPER", "PROPERTY", "BUSINESS" };
        var investmentHoldings = summary.Holdings
            .Where(h => h.AssetClass != null && investmentClasses.Contains(h.AssetClass))
            .GroupBy(h => h.AssetClass!)
            .ToDictionary(g => g.Key, g => g.Sum(h => h.Value));

        decimal totalInvestments = investmentHoldings.Values.Sum();
        bool concentrationRisk = false;
        string? largestConcentration = null;

        if (totalInvestments > 0)
        {
            var largest = investmentHoldings.OrderByDescending(kv => kv.Value).FirstOrDefault();
            decimal pct = largest.Value / totalInvestments * 100;
            if (pct > 40)
            {
                concentrationRisk = true;
                largestConcentration = $"{largest.Key} ({Math.Round(pct, 0)}%)";
            }
        }

        var investments = new InvestmentPositionDto
        {
            TotalValue = Math.Round(totalInvestments, 2),
            ByAssetClass = investmentHoldings.ToDictionary(
                kv => kv.Key, kv => Math.Round(kv.Value, 2)),
            HasConcentrationRisk = concentrationRisk,
            LargestConcentration = largestConcentration
        };

        // ── Asset Efficiency ────────────────────────────────────────────────────
        var vehicles = summary.Holdings
            .Where(h => h.AssetClass == "VEHICLE")
            .ToList();

        var costGeneratingNames = vehicles.Select(v => v.AccountName).ToList();
        decimal costGeneratingValue = vehicles.Sum(v => v.Value);

        // Income-producing: property (if rental), super, business, stocks, ETFs, crypto
        decimal incomeProducingValue = summary.Holdings
            .Where(h => h.AssetClass != null &&
                investmentClasses.Except(new[] { "PROPERTY" }).Contains(h.AssetClass))
            .Sum(h => h.Value);

        var assets = new AssetEfficiencyDto
        {
            IncomeProducingAssetValue = Math.Round(incomeProducingValue, 2),
            CostGeneratingAssetValue = Math.Round(costGeneratingValue, 2),
            VehicleCount = vehicles.Count,
            CostGeneratingAssetNames = costGeneratingNames
        };

        return new FinanceContextDto
        {
            EntityId = entityId,
            AsOf = DateTime.UtcNow,
            Currency = summary.Currency,
            NetWorth = netWorth,
            Cash = cash,
            Spending = spending,
            Saving = saving,
            Liabilities = liabilities,
            Investments = investments,
            Assets = assets
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Cache persistence
    // ────────────────────────────────────────────────────────────────────────────

    private async Task PersistSnapshotAsync(Guid userId, Guid entityId, FinanceContextDto ctx, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(ctx);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

            // Delete old snapshots for this entity
            var old = await _db.AgentContextSnapshots
                .Where(s => s.EntityId == entityId && s.SnapshotType == "full")
                .ToListAsync(ct);
            _db.AgentContextSnapshots.RemoveRange(old);

            _db.AgentContextSnapshots.Add(new AgentContextSnapshot
            {
                UserId = userId,
                EntityId = entityId,
                SnapshotType = "full",
                AsOfDate = ctx.AsOf,
                Payload = payload,
                Hash = hash,
                ExpiresAt = DateTime.UtcNow.Add(SnapshotTtl)
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AgentContext] Failed to persist snapshot for entity {EntityId}", entityId);
        }
    }
}
