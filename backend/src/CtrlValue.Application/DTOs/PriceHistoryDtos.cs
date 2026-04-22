namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// PriceHistory DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class PriceHistoryDto
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string InstrumentSymbol { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePriceHistoryRequest
{
    public Guid InstrumentId { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal? OpenPrice { get; set; }
    public decimal ClosePrice { get; set; }
    public decimal? HighPrice { get; set; }
    public decimal? LowPrice { get; set; }
    public long? Volume { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Source { get; set; }
}

public class BulkPriceImportRequest
{
    public Guid InstrumentId { get; set; }
    public List<PriceDataPoint> Prices { get; set; } = new();
}

public class PriceDataPoint
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public long? Volume { get; set; }
}
