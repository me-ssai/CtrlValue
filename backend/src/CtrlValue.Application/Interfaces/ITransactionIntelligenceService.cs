namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Analyses committed transactions to detect transfers between own accounts,
/// identify recurring subscriptions, and surface spending patterns.
/// All methods are read-only (no mutations) — callers decide whether to apply suggestions.
/// </summary>
public interface ITransactionIntelligenceService
{
    // ── Transfer Detection ───────────────────────────────────────────────────

    /// <summary>
    /// Scans recent unlinked transactions for probable internal transfers
    /// (same entity, opposite directions, matching amount within a tolerance window).
    /// Returns candidate pairs without mutating any rows.
    /// </summary>
    Task<List<TransferCandidateDto>> DetectTransferCandidatesAsync(
        Guid entityId, int lookbackDays = 7);

    /// <summary>
    /// Links two transactions as an internal transfer by assigning them a shared
    /// <c>TransferGroupId</c> and re-typing both as Income/Expense legs.
    /// </summary>
    Task LinkTransferAsync(Guid outflowTxnId, Guid inflowTxnId, Guid entityId);

    /// <summary>
    /// Unlinks a transfer pair — clears <c>TransferGroupId</c> from both legs.
    /// </summary>
    Task UnlinkTransferAsync(Guid transferGroupId, Guid entityId);

    // ── Subscription / Recurring Detection ──────────────────────────────────

    /// <summary>
    /// Identifies recurring transaction patterns (subscriptions, standing orders)
    /// by clustering on normalised merchant name + approximate amount + cadence.
    /// Returns one <see cref="RecurringPatternDto"/> per detected series.
    /// </summary>
    Task<List<RecurringPatternDto>> DetectRecurringPatternsAsync(
        Guid entityId, int lookbackMonths = 6);

    // ── Spending Intelligence ────────────────────────────────────────────────

    /// <summary>
    /// Returns month-over-month spending by category for the last <paramref name="months"/> months.
    /// </summary>
    Task<List<SpendingByMonthDto>> GetSpendingTrendAsync(Guid entityId, int months = 6);

    /// <summary>
    /// Returns the top N merchants by total spend in the given date range.
    /// </summary>
    Task<List<MerchantSpendDto>> GetTopMerchantsAsync(
        Guid entityId, DateTime from, DateTime to, int topN = 10);

    /// <summary>
    /// Returns a cash-flow summary (total income vs total expenses) per month
    /// for the given range. Transfers are excluded.
    /// </summary>
    Task<List<CashFlowMonthDto>> GetCashFlowAsync(Guid entityId, int months = 6);
}

// ── DTOs returned by ITransactionIntelligenceService ────────────────────────

public record TransferCandidateDto(
    Guid OutflowTxnId,
    string OutflowDescription,
    string OutflowAccount,
    DateTime OutflowDate,

    Guid InflowTxnId,
    string InflowDescription,
    string InflowAccount,
    DateTime InflowDate,

    decimal Amount,
    string Currency,

    /// <summary>0–1 confidence score based on amount match, timing, and description similarity.</summary>
    double Confidence
);

public record RecurringPatternDto(
    string MerchantNormalised,
    string? DisplayName,
    decimal TypicalAmount,
    string Currency,
    /// <summary>"Monthly" | "Weekly" | "Fortnightly" | "Annual" | "Irregular"</summary>
    string Cadence,
    int OccurrenceCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    DateTime? PredictedNextDate,
    List<Guid> TransactionIds
);

public record SpendingByMonthDto(
    int Year,
    int Month,
    string CategoryName,
    decimal Total
);

public record MerchantSpendDto(
    string Merchant,
    decimal TotalSpend,
    int TransactionCount,
    string Currency
);

public record CashFlowMonthDto(
    int Year,
    int Month,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Net
);
