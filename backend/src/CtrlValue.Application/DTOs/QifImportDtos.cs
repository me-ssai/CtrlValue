namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// QIF Import DTOs
// ═══════════════════════════════════════════════════════════════════════════

// ── File-level ──────────────────────────────────────────────────────────────

public class ImportedTransactionsFileDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AllowDuplicates { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int AlreadyImportedRows { get; set; }
    public int ErrorRows { get; set; }
}

// ── Staging row ──────────────────────────────────────────────────────────────

public class StagingRowDto
{
    public Guid Id { get; set; }
    public Guid ImportedTransactionsFileId { get; set; }
    public Guid AccountId { get; set; }
    
    public Guid? CounterAccountId { get; set; }
    public string? CounterAccountName { get; set; }
    
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    
    /// <summary>Absolute amount (always positive).</summary>
    public decimal Amount { get; set; }
    
    /// <summary>Signed amount as parsed from QIF. Positive = Inflow, Negative = Outflow.</summary>
    public decimal AmountRaw { get; set; }
    
    public string Status { get; set; } = string.Empty;
    public string? ErrorReason { get; set; }
    
    /// <summary>
    /// Server-computed label. Reflects what the transaction type will be on commit:
    /// "Income", "Expense", "Transfer", "Loan Repayment", or "Loan Disbursement".
    /// </summary>
    public string? InferredType { get; set; }
}

// ── Review wrapper ───────────────────────────────────────────────────────────

public class StagedImportReviewDto
{
    public ImportedTransactionsFileDto File { get; set; } = null!;
    public List<StagingRowDto> ValidRows { get; set; } = new();
    public List<StagingRowDto> DuplicateRows { get; set; } = new();
    public List<StagingRowDto> AlreadyImportedRows { get; set; } = new();
    public List<StagingRowDto> ErrorRows { get; set; } = new();
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public class UpdateStagingRowRequest
{
    /// <summary>
    /// For internal transfer: the opposite account involved in the transaction.
    /// </summary>
    public Guid? CounterAccountId { get; set; }
    
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// If true, moves the row's status from Duplicate or AlreadyImported back to Valid.
    /// </summary>
    public bool? IgnoreDuplicateWarning { get; set; }
}
