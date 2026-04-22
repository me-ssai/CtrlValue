using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

/// <summary>
/// Stores metadata for a real estate asset. Linked 1:1 to an ASSET/PROPERTY Account
/// which holds the financial tracking (balance, valuations, transactions).
/// </summary>
public class Property : BaseEntity
{
    public Guid AccountId { get; set; }
    public Guid EntityId { get; set; }

    // ── Address ──────────────────────────────────────────────────────────────
    public string Address { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string? State { get; set; }
    public string? PostCode { get; set; }
    public string Country { get; set; } = "AUS";

    // ── Classification ───────────────────────────────────────────────────────
    public PropertyType PropertyType { get; set; } = PropertyType.RESIDENTIAL;

    // ── Physical specs (all optional) ────────────────────────────────────────
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? CarSpaces { get; set; }
    public decimal? LandSizeSqm { get; set; }
    public decimal? FloorSizeSqm { get; set; }
    public int? YearBuilt { get; set; }

    // ── Financials ───────────────────────────────────────────────────────────
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }

    // ── Rental ───────────────────────────────────────────────────────────────
    public bool IsRental { get; set; }
    public decimal? WeeklyRentTarget { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Account Account { get; set; } = null!;
}
