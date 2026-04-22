using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

/// <summary>
/// One-to-one with a LIABILITY Account. Stores all loan-specific configuration
/// including rate information, schedule parameters, and links to related accounts.
/// </summary>
public class LoanDetails : BaseEntity
{
    public Guid AccountId { get; set; }
    public Guid EntityId { get; set; }

    // ── Linked accounts ──────────────────────────────────────────────────────
    /// <summary>Optional: asset/property account being financed (for LVR calculation).</summary>
    public Guid? PropertyAccountId { get; set; }
    /// <summary>Optional: offset account that reduces the effective loan balance for interest calculation.</summary>
    public Guid? OffsetAccountId { get; set; }

    // ── Core loan parameters ─────────────────────────────────────────────────
    public decimal LoanAmount { get; set; }
    /// <summary>Annual interest rate expressed as a fraction, e.g. 0.065 = 6.5%.</summary>
    public decimal InterestRate { get; set; }
    public LoanRateType RateType { get; set; } = LoanRateType.Variable;
    public DateTime? FixedRateExpiresAt { get; set; }

    // ── Repayment schedule ───────────────────────────────────────────────────
    public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;
    public decimal RepaymentAmount { get; set; }
    public int LoanTermMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime NextPaymentDate { get; set; }
    public bool IsInterestOnly { get; set; } = false;

    // ── Computed fields (updated by RecalculateRedrawAsync) ──────────────────
    /// <summary>Sum of all extra repayment transactions above the minimum. Available to redraw.</summary>
    public decimal RedrawAvailable { get; set; } = 0;

    public string? Notes { get; set; }

    // ── Navigation properties ────────────────────────────────────────────────
    public Account Account { get; set; } = null!;
    public Account? PropertyAccount { get; set; }
    public Account? OffsetAccount { get; set; }
    public ICollection<LoanRateHistory> RateHistory { get; set; } = new List<LoanRateHistory>();
}
