namespace CtrlValue.Domain.Entities;

/// <summary>
/// Monthly snapshot of an entity's savings behaviour.
/// Persisted each time the agent context is refreshed (or on-demand).
/// Used to render a savings rate trend chart in the agent Overview tab.
/// Follows the same snapshot pattern as Valuation: one row per entity per period.
/// </summary>
public class AgentSavingsSnapshot : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }

    /// <summary>The month this snapshot represents (first day of month, UTC).</summary>
    public DateTime AsOfDate { get; set; }

    public decimal SavingsRatePercent { get; set; }
    public decimal AverageMonthlyIncome { get; set; }
    public decimal AverageMonthlyExpenses { get; set; }
    public decimal AverageMonthlySavings { get; set; }
    public string Currency { get; set; } = "AUD";
}
