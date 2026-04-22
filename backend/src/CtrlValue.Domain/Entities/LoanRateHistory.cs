namespace CtrlValue.Domain.Entities;

/// <summary>
/// Records each time the interest rate changed on a loan. Used to produce
/// historically-accurate amortisation schedules.
/// </summary>
public class LoanRateHistory : BaseEntity
{
    public Guid LoanDetailsId { get; set; }
    /// <summary>Annual rate at this point in time (e.g. 0.065 = 6.5%).</summary>
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public LoanDetails LoanDetails { get; set; } = null!;
}
