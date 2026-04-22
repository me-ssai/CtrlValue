namespace CtrlValue.Domain.Entities;

public class Valuation : BaseEntity
{
    public Guid AccountId { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Source { get; set; }
    public string? Notes { get; set; }
    
    // Navigation properties
    public Account Account { get; set; } = null!;
}
