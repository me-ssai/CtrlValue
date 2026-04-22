using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Property DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class PropertyDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;

    // Address
    public string Address { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? PostCode { get; set; }
    public string Country { get; set; } = "AUS";

    // Classification
    public string PropertyType { get; set; } = string.Empty;

    // Physical specs
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? CarSpaces { get; set; }
    public decimal? LandSizeSqm { get; set; }
    public decimal? FloorSizeSqm { get; set; }
    public int? YearBuilt { get; set; }

    // Financials
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }

    // Rental
    public bool IsRental { get; set; }
    public decimal? WeeklyRentTarget { get; set; }

    // Computed from Account
    public decimal CurrentValue { get; set; }
    public decimal? LatestValuationValue { get; set; }
    public DateTime? LatestValuationAsOfDate { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreatePropertyRequest
{
    // Address
    public string Address { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? PostCode { get; set; }
    public string Country { get; set; } = "AUS";

    // Classification
    public PropertyType PropertyType { get; set; } = PropertyType.RESIDENTIAL;

    // Physical specs (all optional)
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? CarSpaces { get; set; }
    public decimal? LandSizeSqm { get; set; }
    public decimal? FloorSizeSqm { get; set; }
    public int? YearBuilt { get; set; }

    // Financials
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }

    // Rental
    public bool IsRental { get; set; }
    public decimal? WeeklyRentTarget { get; set; }

    // Account — resolved server-side from auth context, ignored if sent by client
    public Guid? EntityId { get; set; }
    public string Currency { get; set; } = "AUD";
}

public class UpdatePropertyRequest
{
    public string Address { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? PostCode { get; set; }
    public string Country { get; set; } = "AUS";
    public PropertyType PropertyType { get; set; } = PropertyType.RESIDENTIAL;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? CarSpaces { get; set; }
    public decimal? LandSizeSqm { get; set; }
    public decimal? FloorSizeSqm { get; set; }
    public int? YearBuilt { get; set; }
    public bool IsRental { get; set; }
    public decimal? WeeklyRentTarget { get; set; }
}
