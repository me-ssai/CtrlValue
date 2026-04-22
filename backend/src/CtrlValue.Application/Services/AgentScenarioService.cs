using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Runs deterministic what-if financial scenarios.
/// All calculations are rule-based math — no LLM calls.
/// </summary>
public class AgentScenarioService : IAgentScenarioService
{
    private readonly AppDbContext _db;
    private readonly IAgentContextBuilderService _contextBuilder;
    private readonly ILogger<AgentScenarioService> _logger;

    public AgentScenarioService(
        AppDbContext db,
        IAgentContextBuilderService contextBuilder,
        ILogger<AgentScenarioService> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<ScenarioResultDto> RunScenarioAsync(
        Guid userId,
        Guid entityId,
        RunScenarioRequest request,
        CancellationToken ct = default)
    {
        var ctx = await _contextBuilder.BuildContextAsync(userId, entityId, forceRefresh: false, ct);

        var result = request.ScenarioType switch
        {
            "CutCategory"         => RunCutCategory(ctx, request),
            "PayOffLoan"          => RunPayOffLoan(ctx, request),
            "IncreaseSavingsRate" => RunIncreaseSavingsRate(ctx, request),
            "SellVehicle"         => RunSellVehicle(ctx, request),
            _ => throw new ArgumentException($"Unknown scenario type: {request.ScenarioType}")
        };

        // Persist scenario run (non-fatal)
        try
        {
            _db.AgentScenarios.Add(new AgentScenario
            {
                UserId          = userId,
                EntityId        = entityId,
                ScenarioType    = result.ScenarioType,
                Title           = result.Title,
                RequestPayload  = JsonSerializer.Serialize(request),
                ResultPayload   = JsonSerializer.Serialize(result),
                Currency        = ctx.Currency,
                TenantId        = ""
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ScenarioService] Failed to persist scenario for entity {EntityId}", entityId);
        }

        return result;
    }

    public async Task<List<AgentScenarioHistoryDto>> GetScenarioHistoryAsync(
        Guid userId,
        Guid entityId,
        int limit = 20)
    {
        var rows = await _db.AgentScenarios
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.EntityId == entityId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return rows.Select(s =>
        {
            ScenarioResultDto? result = null;
            try { result = JsonSerializer.Deserialize<ScenarioResultDto>(s.ResultPayload); }
            catch { /* swallow — return null result */ }

            return new AgentScenarioHistoryDto
            {
                Id           = s.Id,
                ScenarioType = s.ScenarioType,
                Title        = s.Title,
                Result       = result!,
                CreatedAt    = s.CreatedAt
            };
        }).Where(s => s.Result != null).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CutCategory: Reduce spending in a category by X%
    // ─────────────────────────────────────────────────────────────────────────

    private static ScenarioResultDto RunCutCategory(FinanceContextDto ctx, RunScenarioRequest req)
    {
        var categoryName = req.CategoryName ?? throw new ArgumentException("CategoryName is required.");
        var reductionPct = req.ReductionPercent ?? throw new ArgumentException("ReductionPercent is required.");

        if (reductionPct <= 0 || reductionPct > 100)
            throw new ArgumentException("ReductionPercent must be between 1 and 100.");

        var category = ctx.Spending.TopCategories
            .FirstOrDefault(c => c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (category == null)
            throw new ArgumentException($"Category '{categoryName}' not found in spending data.");

        decimal monthlySaving = Math.Round(category.MonthlyAverage * (reductionPct / 100m), 2);
        decimal annualSaving  = Math.Round(monthlySaving * 12, 2);
        decimal newSavingsRate = ctx.Saving.AverageMonthlyIncome > 0
            ? Math.Round((ctx.Saving.AverageMonthlySavings + monthlySaving) / ctx.Saving.AverageMonthlyIncome * 100, 1)
            : 0;

        return new ScenarioResultDto
        {
            ScenarioType = "CutCategory",
            Title = $"Reduce {categoryName} spending by {reductionPct:N0}%",
            Summary = $"If you reduced your {categoryName} spending by {reductionPct:N0}%, " +
                      $"you could free up approximately {ctx.Currency} {monthlySaving:N0}/month. " +
                      $"Over a full year that would be {ctx.Currency} {annualSaving:N0}, " +
                      $"and your estimated savings rate would rise from {ctx.Saving.SavingsRatePercent:N1}% to {newSavingsRate:N1}%.",
            Metrics = new List<ScenarioMetricDto>
            {
                new() { Label = "Current monthly spend", Value = category.MonthlyAverage.ToString("N0"), Unit = $"{ctx.Currency}/mo" },
                new() { Label = "Monthly saving", Value = monthlySaving.ToString("N0"), Unit = $"{ctx.Currency}/mo", IsHighlight = true },
                new() { Label = "Annual saving", Value = annualSaving.ToString("N0"), Unit = ctx.Currency, IsHighlight = true },
                new() { Label = "New savings rate", Value = newSavingsRate.ToString("N1"), Unit = "%" }
            },
            Disclaimer = "Figures are estimates based on average monthly spending. Actual savings may vary."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PayOffLoan: Model interest saved by paying off a loan early
    // ─────────────────────────────────────────────────────────────────────────

    private static ScenarioResultDto RunPayOffLoan(FinanceContextDto ctx, RunScenarioRequest req)
    {
        var loanName = req.LoanName ?? throw new ArgumentException("LoanName is required.");

        var loan = ctx.Liabilities.Liabilities
            .FirstOrDefault(l => l.Name.Equals(loanName, StringComparison.OrdinalIgnoreCase));

        if (loan == null)
            throw new ArgumentException($"Loan '{loanName}' not found.");

        decimal balance          = loan.Balance;
        decimal monthlyRepayment = loan.MonthlyRepayment ?? 0;
        decimal annualRate       = (loan.InterestRate ?? 0) / 100m;
        decimal monthlyRate      = annualRate / 12m;

        // Calculate remaining months to pay off at current repayment
        int remainingMonths = 0;
        decimal totalInterest = 0;

        if (monthlyRate > 0 && monthlyRepayment > 0)
        {
            double n = Math.Log(1 + (double)monthlyRate)
                - Math.Log(1 - (double)(balance * monthlyRate / monthlyRepayment));
            remainingMonths = (int)Math.Ceiling(n / Math.Log(1 + (double)monthlyRate));

            // Sum total interest over remaining term
            decimal runningBalance = balance;
            for (int i = 0; i < remainingMonths && runningBalance > 0; i++)
            {
                decimal interestCharge = Math.Round(runningBalance * monthlyRate, 2);
                totalInterest += interestCharge;
                runningBalance -= (monthlyRepayment - interestCharge);
            }
        }
        else if (monthlyRepayment > 0 && annualRate == 0)
        {
            remainingMonths = (int)Math.Ceiling((double)(balance / monthlyRepayment));
        }

        decimal cashFlowFreed = monthlyRepayment;
        decimal newDebtToIncome = ctx.Saving.AverageMonthlyIncome > 0
            ? Math.Round((ctx.Liabilities.TotalMonthlyRepayments - cashFlowFreed) / ctx.Saving.AverageMonthlyIncome, 2)
            : 0;

        return new ScenarioResultDto
        {
            ScenarioType = "PayOffLoan",
            Title = $"Pay off {loanName} early",
            Summary = $"Paying off {loanName} (balance: {ctx.Currency} {balance:N0}) would eliminate " +
                      $"the monthly repayment of {ctx.Currency} {cashFlowFreed:N0}, free up cash flow, " +
                      (totalInterest > 0
                          ? $"and could save approximately {ctx.Currency} {totalInterest:N0} in interest "
                          : "") +
                      (remainingMonths > 0
                          ? $"over the remaining ~{remainingMonths} months of the loan."
                          : "over the remaining loan term."),
            Metrics = new List<ScenarioMetricDto>
            {
                new() { Label = "Loan balance", Value = balance.ToString("N0"), Unit = ctx.Currency },
                new() { Label = "Monthly repayment freed", Value = cashFlowFreed.ToString("N0"), Unit = $"{ctx.Currency}/mo", IsHighlight = true },
                new() { Label = "Estimated interest saved", Value = totalInterest.ToString("N0"), Unit = ctx.Currency, IsHighlight = totalInterest > 0 },
                new() { Label = "Remaining loan term", Value = remainingMonths > 0 ? remainingMonths.ToString() : "Unknown", Unit = "months" },
                new() { Label = "New debt-to-income ratio", Value = $"{newDebtToIncome * 100:N0}", Unit = "%" }
            },
            Disclaimer = "Interest savings are estimated based on current loan balance and fixed rate. Variable rates may change the outcome."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IncreaseSavingsRate: Project net worth if savings rate improves
    // ─────────────────────────────────────────────────────────────────────────

    private static ScenarioResultDto RunIncreaseSavingsRate(FinanceContextDto ctx, RunScenarioRequest req)
    {
        var targetRate   = req.TargetSavingsRatePercent ?? throw new ArgumentException("TargetSavingsRatePercent is required.");
        var years        = req.ProjectionYears ?? 10;

        if (targetRate <= 0 || targetRate >= 100)
            throw new ArgumentException("TargetSavingsRatePercent must be between 1 and 99.");

        decimal income           = ctx.Saving.AverageMonthlyIncome;
        decimal currentRate      = ctx.Saving.SavingsRatePercent;
        decimal currentMonthlySavings = ctx.Saving.AverageMonthlySavings;
        decimal targetMonthlySavings  = income * (targetRate / 100m);
        decimal monthlyIncrease  = Math.Max(0, targetMonthlySavings - currentMonthlySavings);
        decimal annualIncrease   = monthlyIncrease * 12;

        // Simple compound projection: assume 5% annual return on accumulated savings
        const decimal annualReturnRate = 0.05m;
        decimal currentNetWorth  = ctx.NetWorth.Total;
        decimal currentAnnual    = currentMonthlySavings * 12;
        decimal targetAnnual     = targetMonthlySavings * 12;

        // FV of additional annual savings at 5% for N years
        decimal additionalFv = 0;
        if (annualReturnRate > 0 && annualIncrease > 0)
        {
            additionalFv = Math.Round(
                annualIncrease * ((decimal)Math.Pow((double)(1 + annualReturnRate), years) - 1) / annualReturnRate, 0);
        }

        // FV of current savings
        decimal currentFv = Math.Round(
            currentAnnual * ((decimal)Math.Pow((double)(1 + annualReturnRate), years) - 1) / annualReturnRate, 0);

        decimal totalProjectedNetWorth = Math.Round(currentNetWorth + currentFv + additionalFv, 0);

        return new ScenarioResultDto
        {
            ScenarioType = "IncreaseSavingsRate",
            Title = $"Increase savings rate to {targetRate:N0}%",
            Summary = $"Increasing your savings rate from {currentRate:N1}% to {targetRate:N0}% would require saving " +
                      $"an additional {ctx.Currency} {monthlyIncrease:N0}/month ({ctx.Currency} {annualIncrease:N0}/year). " +
                      $"Projected over {years} years at 5% annual return, this could contribute an additional " +
                      $"{ctx.Currency} {additionalFv:N0} to your net worth.",
            Metrics = new List<ScenarioMetricDto>
            {
                new() { Label = "Current savings rate", Value = currentRate.ToString("N1"), Unit = "%" },
                new() { Label = "Target savings rate", Value = targetRate.ToString("N1"), Unit = "%" },
                new() { Label = "Additional monthly saving", Value = monthlyIncrease.ToString("N0"), Unit = $"{ctx.Currency}/mo", IsHighlight = true },
                new() { Label = $"Additional wealth in {years}yr", Value = additionalFv.ToString("N0"), Unit = ctx.Currency, IsHighlight = true },
                new() { Label = $"Projected net worth in {years}yr", Value = totalProjectedNetWorth.ToString("N0"), Unit = ctx.Currency }
            },
            Disclaimer = $"Projection assumes {years} years, 5% annual return, and constant income/expenses. " +
                         "This is illustrative only and not a guarantee of investment returns."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SellVehicle: Model the cash release from selling a vehicle
    // ─────────────────────────────────────────────────────────────────────────

    private static ScenarioResultDto RunSellVehicle(FinanceContextDto ctx, RunScenarioRequest req)
    {
        var vehicleName = req.VehicleName ?? throw new ArgumentException("VehicleName is required.");

        var vehicle = ctx.Assets.CostGeneratingAssetNames
            .FirstOrDefault(n => n.Equals(vehicleName, StringComparison.OrdinalIgnoreCase));

        if (vehicle == null)
            throw new ArgumentException($"Vehicle '{vehicleName}' not found in tracked assets.");

        // We don't have per-vehicle value, so estimate from CostGeneratingAssetValue / VehicleCount
        decimal estimatedValue = ctx.Assets.VehicleCount > 0
            ? Math.Round(ctx.Assets.CostGeneratingAssetValue / ctx.Assets.VehicleCount, 0)
            : 0;

        // Estimated ongoing savings: vehicles cost roughly 15–20% of their value per year in AU
        decimal estimatedAnnualCostSaving = Math.Round(estimatedValue * 0.15m, 0);
        decimal estimatedMonthlyCostSaving = Math.Round(estimatedAnnualCostSaving / 12, 0);

        decimal newCostGeneratingValue  = ctx.Assets.CostGeneratingAssetValue - estimatedValue;
        int remainingVehicles           = ctx.Assets.VehicleCount - 1;
        decimal newNetWorthEstimate     = ctx.NetWorth.Total; // cash replaces vehicle value

        return new ScenarioResultDto
        {
            ScenarioType = "SellVehicle",
            Title = $"Sell {vehicleName}",
            Summary = $"Selling {vehicleName} could release approximately {ctx.Currency} {estimatedValue:N0} in cash. " +
                      $"Additionally, removing this vehicle could reduce ongoing costs (insurance, maintenance, registration) " +
                      $"by an estimated {ctx.Currency} {estimatedMonthlyCostSaving:N0}/month ({ctx.Currency} {estimatedAnnualCostSaving:N0}/year). " +
                      (remainingVehicles > 0
                          ? $"You would still have {remainingVehicles} vehicle(s) remaining."
                          : "You would have no vehicles remaining."),
            Metrics = new List<ScenarioMetricDto>
            {
                new() { Label = "Estimated vehicle value", Value = estimatedValue.ToString("N0"), Unit = ctx.Currency, IsHighlight = true },
                new() { Label = "Est. monthly cost saving", Value = estimatedMonthlyCostSaving.ToString("N0"), Unit = $"{ctx.Currency}/mo", IsHighlight = true },
                new() { Label = "Est. annual cost saving", Value = estimatedAnnualCostSaving.ToString("N0"), Unit = ctx.Currency },
                new() { Label = "Remaining vehicles", Value = remainingVehicles.ToString(), Unit = "" }
            },
            Disclaimer = "Vehicle value is estimated as an average across all tracked vehicles. " +
                         "Ongoing cost savings are estimated at 15% of vehicle value per year. Actual costs vary significantly."
        };
    }
}
