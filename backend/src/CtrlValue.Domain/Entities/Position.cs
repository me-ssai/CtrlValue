using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Position : BaseEntity
{
    public Guid AccountId { get; set; }
    public Guid? InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public MetalUnit Unit { get; set; } = MetalUnit.UNIT;
    public decimal? CostBasisTotal { get; set; }
    public DateTime OpenedAt { get; set; }
    
    // Navigation properties
    public Account Account { get; set; } = null!;
    public Instrument? Instrument { get; set; }
}
