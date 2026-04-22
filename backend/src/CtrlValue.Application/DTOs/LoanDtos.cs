namespace CtrlValue.Application.DTOs;

// ── Read DTOs ─────────────────────────────────────────────────────────────────

public class LoanDetailsDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    public Guid? PropertyAccountId { get; set; }
    public string? PropertyAccountName { get; set; }
    public Guid? OffsetAccountId { get; set; }
    public string? OffsetAccountName { get; set; }

    public decimal LoanAmount { get; set; }
    public decimal InterestRate { get; set; }
    public string RateType { get; set; } = "Variable";
    public DateTime? FixedRateExpiresAt { get; set; }

    public string PaymentFrequency { get; set; } = "Monthly";
    public decimal RepaymentAmount { get; set; }
    public int LoanTermMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime NextPaymentDate { get; set; }
    public bool IsInterestOnly { get; set; }
    public decimal RedrawAvailable { get; set; }
    public string? Notes { get; set; }

    public List<LoanRateHistoryDto> RateHistory { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class LoanRateHistoryDto
{
    public Guid Id { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Loan Summary (dashboard card data) ───────────────────────────────────────

public class LoanSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal RemainingBalance { get; set; }
    public decimal CurrentInterestRate { get; set; }
    public string RateType { get; set; } = "Variable";
    public DateTime? FixedRateExpiresAt { get; set; }
    /// <summary>Days until the fixed rate expires. Null if variable or field not set.</summary>
    public int? DaysUntilFixedRateExpiry { get; set; }
    public decimal NextPaymentAmount { get; set; }
    public DateTime NextPaymentDate { get; set; }
    /// <summary>Days until the next payment is due.</summary>
    public int DaysUntilNextPayment { get; set; }
    public decimal RedrawAvailable { get; set; }
    /// <summary>Loan-to-Value Ratio. Null if no property account linked or no valuation exists.</summary>
    public decimal? LVR { get; set; }
    public decimal? PropertyValue { get; set; }
    public int MonthsRemaining { get; set; }
    public decimal TotalInterestPayable { get; set; }

    // Offset account
    public Guid? OffsetAccountId { get; set; }
    public string? OffsetAccountName { get; set; }
    public decimal OffsetBalance { get; set; }
}

// ── Amortisation Schedule ─────────────────────────────────────────────────────

public class AmortisationScheduleDto
{
    /// <summary>Standard schedule (no extra payments).</summary>
    public List<AmortisationRowDto> Standard { get; set; } = new();
    /// <summary>Accelerated schedule with extra payment applied. Same as Standard if extraPayment == 0.</summary>
    public List<AmortisationRowDto> Accelerated { get; set; } = new();
    public decimal ExtraPaymentPerPeriod { get; set; }
    public int MonthsSaved { get; set; }
    public decimal InterestSaved { get; set; }
}

public class AmortisationRowDto
{
    public int PaymentNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal CumulativeInterest { get; set; }
    public decimal Balance { get; set; }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public class CreateLoanDetailsRequest
{
    public Guid AccountId { get; set; }
    public Guid? PropertyAccountId { get; set; }
    public Guid? OffsetAccountId { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal InterestRate { get; set; }
    public string RateType { get; set; } = "Variable";
    public DateTime? FixedRateExpiresAt { get; set; }
    public string PaymentFrequency { get; set; } = "Monthly";
    public decimal RepaymentAmount { get; set; }
    public int LoanTermMonths { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsInterestOnly { get; set; } = false;
    public string? Notes { get; set; }
}

public class UpdateLoanDetailsRequest
{
    public Guid? PropertyAccountId { get; set; }
    public Guid? OffsetAccountId { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal InterestRate { get; set; }
    public string RateType { get; set; } = "Variable";
    public DateTime? FixedRateExpiresAt { get; set; }
    public string PaymentFrequency { get; set; } = "Monthly";
    public decimal RepaymentAmount { get; set; }
    public int LoanTermMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime NextPaymentDate { get; set; }
    public bool IsInterestOnly { get; set; } = false;
    public string? Notes { get; set; }
}

public class LoanRateChangeRequest
{
    public decimal Rate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string? Notes { get; set; }
}
