namespace CtrlValue.Domain.Entities;

public class PriceHistory : BaseEntity
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
    
    // Navigation properties
    public Instrument Instrument { get; set; } = null!;
}
