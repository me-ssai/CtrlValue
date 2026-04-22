using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentInsightService : IAgentInsightService
{
    private readonly AppDbContext _db;
    private readonly IAgentContextBuilderService _contextBuilder;
    private readonly ILogger<AgentInsightService> _logger;

    public AgentInsightService(
        AppDbContext db,
        IAgentContextBuilderService contextBuilder,
        ILogger<AgentInsightService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<List<AgentInsightDto>> GetInsightsAsync(Guid userId, Guid entityId)
    {
        var insights = await _db.AgentInsights
            .AsNoTracking()
            .Where(i => i.EntityId == entityId && !i.IsDismissed)
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.GeneratedAt)
            .ToListAsync();

        return insights.Select(Map).ToList();
    }

    public async Task RefreshInsightsAsync(Guid userId, Guid entityId, CancellationToken ct = default)
    {
        var ctx = await _contextBuilder.BuildContextAsync(userId, entityId, forceRefresh: false, ct);

        var detectedInsights = DetectInsights(userId, entityId, ctx);

        foreach (var insight in detectedInsights)
        {
            // Upsert: replace any existing undismissed insight of the same type
            var existing = await _db.AgentInsights
                .Where(i => i.EntityId == entityId
                    && i.InsightType == insight.InsightType
                    && !i.IsDismissed
                    && !i.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                existing.Title = insight.Title;
                existing.Summary = insight.Summary;
                existing.Severity = insight.Severity;
                existing.Evidence = insight.Evidence;
                existing.GeneratedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.AgentInsights.Add(insight);
            }
        }

        // Remove insights whose condition is no longer triggered
        var detectedTypes = detectedInsights.Select(i => i.InsightType).ToHashSet();
        var staleInsights = await _db.AgentInsights
            .Where(i => i.EntityId == entityId && !i.IsDismissed && !i.IsDeleted)
            .Where(i => !detectedTypes.Contains(i.InsightType))
            .ToListAsync(ct);

        foreach (var stale in staleInsights)
        {
            stale.IsDeleted = true;
            stale.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[AgentInsights] Refreshed {Count} insights for entity {EntityId}",
            detectedInsights.Count, entityId);
    }

    public async Task DismissInsightAsync(Guid insightId, Guid userId)
    {
        var insight = await _db.AgentInsights
            .FirstOrDefaultAsync(i => i.Id == insightId && i.UserId == userId)
            ?? throw new KeyNotFoundException("Insight not found.");

        insight.IsDismissed = true;
        insight.DismissedAt = DateTime.UtcNow;
        insight.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Rule-based insight detection
    // ────────────────────────────────────────────────────────────────────────────

    private List<AgentInsight> DetectInsights(Guid userId, Guid entityId, FinanceContextDto ctx)
    {
        var insights = new List<AgentInsight>();
        var now = DateTime.UtcNow;

        // ── Idle Cash ───────────────────────────────────────────────────────────
        if (ctx.Cash.EstimatedIdleCash > 10_000)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.IdleCash,
                AgentInsightSeverity.Warning,
                "Idle cash above threshold",
                $"Approximately {ctx.Currency} {ctx.Cash.EstimatedIdleCash:N0} in cash may be sitting idle " +
                $"above your estimated 3-month buffer ({ctx.Currency} {ctx.Cash.EmergencyFundEstimate:N0}). " +
                "Idle cash generally loses purchasing power to inflation over time.",
                $"[{{\"label\":\"Idle cash\",\"value\":\"{ctx.Cash.EstimatedIdleCash:N0}\",\"unit\":\"{ctx.Currency}\"}}," +
                $"{{\"label\":\"Emergency buffer\",\"value\":\"{ctx.Cash.EmergencyFundEstimate:N0}\",\"unit\":\"{ctx.Currency}\"}}]",
                now));
        }

        // ── Low Savings Rate ────────────────────────────────────────────────────
        if (ctx.Saving.SavingsRatePercent < 0)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.LowSavingsRate,
                AgentInsightSeverity.Alert,
                "Negative savings rate",
                $"Your average monthly expenses ({ctx.Currency} {ctx.Saving.AverageMonthlyExpenses:N0}) " +
                $"are exceeding your income ({ctx.Currency} {ctx.Saving.AverageMonthlyIncome:N0}). " +
                "This means net worth is generally declining over time.",
                $"[{{\"label\":\"Savings rate\",\"value\":\"{ctx.Saving.SavingsRatePercent:N1}\",\"unit\":\"%\"}}]",
                now));
        }
        else if (ctx.Saving.SavingsRatePercent < 10 && ctx.Saving.MonthsAnalysed > 0)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.LowSavingsRate,
                AgentInsightSeverity.Warning,
                "Low savings rate",
                $"Your savings rate is {ctx.Saving.SavingsRatePercent:N1}% over the last " +
                $"{ctx.Saving.MonthsAnalysed} months. A common guideline is to aim for 15–20% or more " +
                "to build long-term wealth.",
                $"[{{\"label\":\"Savings rate\",\"value\":\"{ctx.Saving.SavingsRatePercent:N1}\",\"unit\":\"%\"}}," +
                $"{{\"label\":\"Monthly savings\",\"value\":\"{ctx.Saving.AverageMonthlySavings:N0}\",\"unit\":\"{ctx.Currency}\"}}]",
                now));
        }

        // ── Subscription Creep ──────────────────────────────────────────────────
        if (ctx.Spending.MonthlySubscriptionTotal > 200)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.SubscriptionCreep,
                AgentInsightSeverity.Info,
                "Recurring subscriptions worth reviewing",
                $"Your detected recurring subscriptions total approximately {ctx.Currency} {ctx.Spending.MonthlySubscriptionTotal:N0}/month " +
                $"across {ctx.Spending.Subscriptions.Count} services. " +
                "Reviewing these periodically may surface unused or duplicated subscriptions.",
                $"[{{\"label\":\"Monthly subscription total\",\"value\":\"{ctx.Spending.MonthlySubscriptionTotal:N0}\",\"unit\":\"{ctx.Currency}/month\"}}," +
                $"{{\"label\":\"Subscription count\",\"value\":\"{ctx.Spending.Subscriptions.Count}\",\"unit\":\"\"}}]",
                now));
        }

        // ── High Spend Category ─────────────────────────────────────────────────
        var highSpend = ctx.Spending.TopCategories
            .FirstOrDefault(c => c.PercentOfTotal > 30 && c.CategoryName != "Uncategorised");

        if (highSpend != null)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.HighSpendCategory,
                AgentInsightSeverity.Info,
                $"High spend concentration: {highSpend.CategoryName}",
                $"\"{highSpend.CategoryName}\" accounts for {highSpend.PercentOfTotal:N0}% of your average monthly spend " +
                $"({ctx.Currency} {highSpend.MonthlyAverage:N0}/month). " +
                "High category concentration may be worth reviewing depending on whether it is essential or discretionary.",
                $"[{{\"label\":\"{highSpend.CategoryName} spend\",\"value\":\"{highSpend.MonthlyAverage:N0}\",\"unit\":\"{ctx.Currency}/month\"}}," +
                $"{{\"label\":\"Share of total\",\"value\":\"{highSpend.PercentOfTotal:N0}\",\"unit\":\"%\"}}]",
                now));
        }

        // ── Liability Drag ──────────────────────────────────────────────────────
        if (ctx.Liabilities.DebtToIncomeRatio > 0.4m && ctx.Liabilities.TotalMonthlyRepayments > 0)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.LiabilityDrag,
                AgentInsightSeverity.Warning,
                "High debt-to-income ratio",
                $"Monthly debt repayments ({ctx.Currency} {ctx.Liabilities.TotalMonthlyRepayments:N0}) represent " +
                $"{ctx.Liabilities.DebtToIncomeRatio * 100:N0}% of average monthly income. " +
                "A high debt-service ratio reduces the income available for saving and investing.",
                $"[{{\"label\":\"Monthly repayments\",\"value\":\"{ctx.Liabilities.TotalMonthlyRepayments:N0}\",\"unit\":\"{ctx.Currency}/month\"}}," +
                $"{{\"label\":\"Debt-to-income ratio\",\"value\":\"{ctx.Liabilities.DebtToIncomeRatio * 100:N0}\",\"unit\":\"%\"}}]",
                now));
        }

        // ── Concentration Risk ──────────────────────────────────────────────────
        if (ctx.Investments.HasConcentrationRisk && ctx.Investments.LargestConcentration != null)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.ConcentrationRisk,
                AgentInsightSeverity.Warning,
                "Investment concentration risk",
                $"Your investment portfolio shows concentration in {ctx.Investments.LargestConcentration}. " +
                "High concentration in a single asset class can amplify risk if that class underperforms. " +
                "Diversification is generally considered a way to manage volatility.",
                $"[{{\"label\":\"Largest concentration\",\"value\":\"{ctx.Investments.LargestConcentration}\",\"unit\":\"\"}}," +
                $"{{\"label\":\"Total investments\",\"value\":\"{ctx.Investments.TotalValue:N0}\",\"unit\":\"{ctx.Currency}\"}}]",
                now));
        }

        // ── Non-Income Asset Drag ───────────────────────────────────────────────
        if (ctx.Assets.CostGeneratingAssetValue > 5_000 && ctx.NetWorth.Total > 0)
        {
            decimal costPct = Math.Round(ctx.Assets.CostGeneratingAssetValue / ctx.NetWorth.Total * 100, 1);
            if (costPct > 15)
            {
                var names = ctx.Assets.CostGeneratingAssetNames.Count > 0
                    ? string.Join(", ", ctx.Assets.CostGeneratingAssetNames)
                    : "vehicles";

                insights.Add(Create(userId, entityId, AgentInsightType.NonIncomeAsset,
                    AgentInsightSeverity.Info,
                    "Cost-generating assets represent a large share of net worth",
                    $"Assets that typically generate ongoing costs ({names}) account for " +
                    $"approximately {costPct}% of your net worth ({ctx.Currency} {ctx.Assets.CostGeneratingAssetValue:N0}). " +
                    "These assets generally depreciate and incur insurance, maintenance, and registration costs " +
                    "without producing income. This may be worth reviewing in the context of your overall financial strategy.",
                    $"[{{\"label\":\"Cost-generating value\",\"value\":\"{ctx.Assets.CostGeneratingAssetValue:N0}\",\"unit\":\"{ctx.Currency}\"}}," +
                    $"{{\"label\":\"Share of net worth\",\"value\":\"{costPct}\",\"unit\":\"%\"}}]",
                    now));
            }
        }

        // ── Lifestyle Creep ─────────────────────────────────────────────────────
        if (ctx.Spending.SpendingGrowthPercent.HasValue && ctx.Spending.SpendingGrowthPercent.Value > 10)
        {
            var growth = ctx.Spending.SpendingGrowthPercent.Value;
            insights.Add(Create(userId, entityId, AgentInsightType.LifestyleCreep,
                AgentInsightSeverity.Info,
                "Spending has grown faster than expected",
                $"Your average monthly spending in the most recent 3 months is approximately {growth}% higher " +
                "than the prior 3 months. This pattern — sometimes called lifestyle creep — could gradually erode " +
                "your savings rate over time. It may be worth reviewing whether recent spending increases align " +
                "with income growth or deliberate lifestyle choices.",
                $"[{{\"label\":\"Spending growth\",\"value\":\"{growth}\",\"unit\":\"%\"}}," +
                $"{{\"label\":\"Trend direction\",\"value\":\"{ctx.Spending.TrendDirection}\",\"unit\":\"\"}}]",
                now));
        }

        // ── High Interest Debt (Section E) ──────────────────────────────────────
        var highInterestLoans = ctx.Liabilities.Liabilities
            .Where(l => l.HasLoanDetails && l.InterestRate.HasValue
                && l.InterestRate.Value > 8m
                && l.Balance > 1_000)
            .OrderByDescending(l => l.InterestRate)
            .ToList();

        if (highInterestLoans.Count > 0)
        {
            var topLoan = highInterestLoans[0];
            var loanNames = string.Join(", ", highInterestLoans.Select(l => $"{l.Name} ({l.InterestRate:N1}%)"));
            insights.Add(Create(userId, entityId, AgentInsightType.HighInterestDebt,
                AgentInsightSeverity.Warning,
                "High interest rate debt detected",
                $"You have {highInterestLoans.Count} loan(s) with interest rates above 8%: {loanNames}. " +
                "High interest debt typically costs more over time than conservative investment returns, " +
                "and prioritising repayment may improve overall financial position.",
                $"[{{\"label\":\"Highest rate loan\",\"value\":\"{topLoan.Name}\",\"unit\":\"\"}}," +
                $"{{\"label\":\"Interest rate\",\"value\":\"{topLoan.InterestRate:N1}\",\"unit\":\"%\"}}," +
                $"{{\"label\":\"Balance\",\"value\":\"{topLoan.Balance:N0}\",\"unit\":\"{ctx.Currency}\"}}]",
                now));
        }

        // ── Multiple Vehicles (Section E) ───────────────────────────────────────
        if (ctx.Assets.VehicleCount > 1)
        {
            insights.Add(Create(userId, entityId, AgentInsightType.MultipleVehicles,
                AgentInsightSeverity.Info,
                $"{ctx.Assets.VehicleCount} vehicles detected",
                $"You have {ctx.Assets.VehicleCount} vehicles tracked (total value: {ctx.Currency} {ctx.Assets.CostGeneratingAssetValue:N0}). " +
                "Each additional vehicle generally adds ongoing costs such as insurance, registration, fuel, and maintenance. " +
                "If one or more is rarely used, it may be worth evaluating whether the holding cost is worthwhile.",
                $"[{{\"label\":\"Vehicle count\",\"value\":\"{ctx.Assets.VehicleCount}\",\"unit\":\"\"}}," +
                $"{{\"label\":\"Total vehicle value\",\"value\":\"{ctx.Assets.CostGeneratingAssetValue:N0}\",\"unit\":\"{ctx.Currency}\"}}]",
                now));
        }

        return insights;
    }

    private static AgentInsight Create(
        Guid userId, Guid entityId,
        AgentInsightType type, AgentInsightSeverity severity,
        string title, string summary, string evidence,
        DateTime now) => new()
    {
        UserId = userId,
        EntityId = entityId,
        InsightType = type,
        Severity = severity,
        Title = title,
        Summary = summary,
        Evidence = evidence,
        SourceType = AgentInsightSourceType.Internal,
        IsDismissed = false,
        GeneratedAt = now
    };

    private static AgentInsightDto Map(AgentInsight i) => new()
    {
        Id = i.Id,
        InsightType = i.InsightType.ToString(),
        Severity = i.Severity.ToString(),
        Title = i.Title,
        Summary = i.Summary,
        Evidence = i.Evidence,
        SourceType = i.SourceType.ToString(),
        IsDismissed = i.IsDismissed,
        GeneratedAt = i.GeneratedAt
    };
}
