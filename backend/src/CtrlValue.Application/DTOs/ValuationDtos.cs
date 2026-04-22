using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Valuation DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class ValuationDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateValuationRequest
{
    public Guid AccountId { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public class UpdateValuationRequest
{
    public decimal Value { get; set; }
    public DateTime? AsOfDate { get; set; }
    public string? Notes { get; set; }
    public string? Source { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// DepreciationSchedule DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class DepreciationScheduleDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? UsefulLifeYears { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? AnnualDepreciationRate { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? AccumulatedDepreciation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDepreciationScheduleRequest
{
    public Guid AccountId { get; set; }
    public DepreciationMethod Method { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? UsefulLifeYears { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? AnnualDepreciationRate { get; set; }
}

public class UpdateDepreciationScheduleRequest
{
    public int? UsefulLifeYears { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? AnnualDepreciationRate { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Budget DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class BudgetDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    public decimal? ActualSpent { get; set; }
    public decimal? Remaining { get; set; }
    public decimal? PercentUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBudgetRequest
{
    public Guid CategoryId { get; set; }
    public BudgetPeriodType PeriodType { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
}

public class UpdateBudgetRequest
{
    public decimal Amount { get; set; }
    public DateTime? PeriodEnd { get; set; }
}
