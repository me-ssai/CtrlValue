using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Budget : BaseEntity
{
    public Guid EntityId { get; set; }
    public Guid CategoryId { get; set; }
    public BudgetPeriodType PeriodType { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    
    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
