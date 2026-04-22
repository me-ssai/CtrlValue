using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class DepreciationSchedule : BaseEntity
{
    public Guid AccountId { get; set; }
    public DepreciationMethod Method { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public int? UsefulLifeYears { get; set; }
    public decimal? SalvageValue { get; set; }
    public decimal? AnnualDepreciationRate { get; set; }
    
    // Navigation properties
    public Account Account { get; set; } = null!;
}
