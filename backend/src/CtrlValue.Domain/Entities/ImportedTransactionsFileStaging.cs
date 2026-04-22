using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class ImportedTransactionsFileStaging : BaseEntity
{
    public Guid ImportedTransactionsFileId { get; set; }
    public Guid EntityId { get; set; }
    
    /// <summary>The primary import account (from the file-level selection).</summary>
    public Guid AccountId { get; set; }
    
    /// <summary>
    /// User-selected secondary account.
    /// Used to link a counter-party account to form a transfer.
    /// </summary>
    public Guid? CounterAccountId { get; set; }
    
    /// <summary>Optional category selected during review.</summary>
    public Guid? CategoryId { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    
    /// <summary>Absolute amount (always positive).</summary>
    public decimal Amount { get; set; }
    
    /// <summary>Signed amount as parsed directly from QIF (negative = outflow, positive = inflow).</summary>
    public decimal AmountRaw { get; set; }
    
    /// <summary>SHA-256 hash used for duplicate detection.</summary>
    public string Hash { get; set; } = string.Empty;
    
    // ── OFX-specific fields ──────────────────────────────────────────────────
    
    /// <summary>Raw FITID from OFX. Used as the primary dedupe key when present; null for QIF imports.</summary>
    public string? ExternalId { get; set; }
    
    /// <summary>Currency code from OFX CURDEF (e.g. AUD, USD). Null for QIF imports.</summary>
    public string? Currency { get; set; }
    
    /// <summary>OFX TRNTYPE (DEBIT, CREDIT, etc.). Diagnostic only; not used for direction logic.</summary>
    public string? OfxTrnType { get; set; }
    
    public StagingStatus Status { get; set; } = StagingStatus.Valid;
    public string? ErrorReason { get; set; }
    
    // Navigation properties
    public ImportedTransactionsFile ImportFile { get; set; } = null!;
    public Account PrimaryAccount { get; set; } = null!;
    public Account? CounterAccount { get; set; }
    public Category? Category { get; set; }
}
