using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Instrument : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType InstrumentType { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Exchange { get; set; }

    /// <summary>Symbol as recognised by the external data provider (e.g. "AAPL" on Yahoo Finance, "XAU" on metals-api). Differs from Symbol when the provider uses a different identifier format.</summary>
    public string? ExternalSymbol { get; set; }
    /// <summary>Which external API the background price-fetch job should call for this instrument. Null means no automatic fetching — prices are entered manually.</summary>
    public PriceProviderType? PriceProvider { get; set; }
    /// <summary>Unit that external price quotes use. TROY_OZ for precious metals; UNIT (default) for stocks/ETFs/crypto where price is per share/token.</summary>
    public MetalUnit PriceUnit { get; set; } = MetalUnit.UNIT;

    // ── Bond / Fixed-income fields (nullable — only relevant for BOND instruments) ──
    /// <summary>Entity that issued the bond (e.g. "Australian Government", "Commonwealth Bank").</summary>
    public string? Issuer { get; set; }
    /// <summary>Par / face value of a single bond unit.</summary>
    public decimal? FaceValue { get; set; }
    /// <summary>Annual coupon rate as a percentage (e.g. 4.25 means 4.25%).</summary>
    public decimal? CouponRate { get; set; }
    /// <summary>How often coupons are paid: Monthly, Quarterly, Semi-annual, Annual.</summary>
    public string? CouponFrequency { get; set; }
    /// <summary>Date the bond matures and principal is repaid.</summary>
    public DateTime? MaturityDate { get; set; }
    /// <summary>Date the bond was originally issued.</summary>
    public DateTime? IssueDate { get; set; }
    /// <summary>Credit rating assigned by a ratings agency (e.g. AAA, AA+, BBB-).</summary>
    public string? CreditRating { get; set; }

    // ── ETF / Fund fields (nullable — only relevant for ETF or FUND instruments) ──
    /// <summary>Annual management expense ratio as a percentage (e.g. 0.07 means 0.07%).</summary>
    public decimal? ExpenseRatio { get; set; }
    /// <summary>Distribution (dividend) yield as a percentage.</summary>
    public decimal? DistributionYield { get; set; }
    /// <summary>How often distributions are paid: Monthly, Quarterly, Semi-annual, Annual.</summary>
    public string? DistributionFrequency { get; set; }
    /// <summary>The index or benchmark this ETF/fund tracks (e.g. "S&P/ASX 200", "S&P 500").</summary>
    public string? UnderlyingIndex { get; set; }

    /// <summary>True for the curated platform-default tickers shown to all users before they add personal holdings.</summary>
    public bool IsDefault { get; set; } = false;

    // Navigation properties
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<PriceHistory> PriceHistory { get; set; } = new List<PriceHistory>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
