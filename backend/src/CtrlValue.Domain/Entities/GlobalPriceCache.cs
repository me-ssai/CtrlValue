using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

/// <summary>
/// Cross-tenant price cache populated by the background price-fetch job.
/// Not tenant-scoped — one record per symbol per day shared across all tenants.
/// Manual PriceHistory records per instrument take precedence over this cache
/// when computing position values.
/// </summary>
public class GlobalPriceCache
{
    public Guid Id { get; set; }

    /// <summary>The instrument symbol as used by the external provider (e.g. "AAPL", "XAU", "bitcoin").</summary>
    public string Symbol { get; set; } = string.Empty;

    public InstrumentType InstrumentType { get; set; }

    public DateTime AsOfDate { get; set; }

    /// <summary>Closing / spot price in the quoted currency.</summary>
    public decimal Price { get; set; }

    /// <summary>Unit that this price is quoted per (TROY_OZ for metals, UNIT for stocks/crypto).</summary>
    public MetalUnit PriceUnit { get; set; } = MetalUnit.UNIT;

    public string Currency { get; set; } = "AUD";

    public PriceProviderType Source { get; set; }

    /// <summary>When the price-fetch job last wrote this record. Used for cache staleness checks.</summary>
    public DateTime FetchedAt { get; set; }
}
