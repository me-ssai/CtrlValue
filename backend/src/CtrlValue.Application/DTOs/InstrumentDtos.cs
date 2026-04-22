using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Instrument DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class InstrumentDto
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstrumentType { get; set; } = string.Empty;
    public string Currency { get; set; } = "AUD";
    public string? Exchange { get; set; }
    public string? ExternalSymbol { get; set; }
    public string? PriceProvider { get; set; }
    public string PriceUnit { get; set; } = "UNIT";
    public decimal? LatestPrice { get; set; }
    public DateTime? LatestPriceDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Bond fields
    public string? Issuer { get; set; }
    public decimal? FaceValue { get; set; }
    public decimal? CouponRate { get; set; }
    public string? CouponFrequency { get; set; }
    public DateTime? MaturityDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? CreditRating { get; set; }

    // ETF / Fund fields
    public decimal? ExpenseRatio { get; set; }
    public decimal? DistributionYield { get; set; }
    public string? DistributionFrequency { get; set; }
    public string? UnderlyingIndex { get; set; }
}

public class CreateInstrumentRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType InstrumentType { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Exchange { get; set; }
    public string? ExternalSymbol { get; set; }
    public PriceProviderType? PriceProvider { get; set; }
    public MetalUnit PriceUnit { get; set; } = MetalUnit.UNIT;

    // Bond fields
    public string? Issuer { get; set; }
    public decimal? FaceValue { get; set; }
    public decimal? CouponRate { get; set; }
    public string? CouponFrequency { get; set; }
    public DateTime? MaturityDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? CreditRating { get; set; }

    // ETF / Fund fields
    public decimal? ExpenseRatio { get; set; }
    public decimal? DistributionYield { get; set; }
    public string? DistributionFrequency { get; set; }
    public string? UnderlyingIndex { get; set; }
}

public class UpdateInstrumentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "AUD";
    public string? Exchange { get; set; }
    public string? ExternalSymbol { get; set; }
    public PriceProviderType? PriceProvider { get; set; }
    public MetalUnit PriceUnit { get; set; } = MetalUnit.UNIT;

    // Bond fields
    public string? Issuer { get; set; }
    public decimal? FaceValue { get; set; }
    public decimal? CouponRate { get; set; }
    public string? CouponFrequency { get; set; }
    public DateTime? MaturityDate { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? CreditRating { get; set; }

    // ETF / Fund fields
    public decimal? ExpenseRatio { get; set; }
    public decimal? DistributionYield { get; set; }
    public string? DistributionFrequency { get; set; }
    public string? UnderlyingIndex { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Position DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class PositionDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid? InstrumentId { get; set; }
    public string? InstrumentSymbol { get; set; }
    public string? InstrumentName { get; set; }
    public string? InstrumentType { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "UNIT";
    public decimal? CostBasisTotal { get; set; }
    public decimal? CostBasisPerUnit { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? UnrealizedGainLoss { get; set; }
    public decimal? UnrealizedGainLossPercent { get; set; }
    public string Currency { get; set; } = "AUD";
    public DateTime OpenedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePositionRequest
{
    public Guid AccountId { get; set; }
    public Guid? InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public MetalUnit Unit { get; set; } = MetalUnit.UNIT;
    public decimal? CostBasisTotal { get; set; }
    public DateTime? OpenedAt { get; set; }
}

public class UpdatePositionRequest
{
    public decimal Quantity { get; set; }
    public decimal? CostBasisTotal { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Instrument Search
// ═══════════════════════════════════════════════════════════════════════════

public record InstrumentSearchResultDto(
    string Symbol,
    string Name,
    string Type,
    string? Exchange,
    string Currency,
    bool IsAlreadyTracked
);
