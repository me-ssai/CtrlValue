using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid EntityId { get; set; }
    public DateTime TxnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType TxnType { get; set; }
    
    // Ledger fields
    public Guid AccountId { get; set; }
    
    // Money fields
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    
    // Instrument fields (for BUY/SELL)
    public Guid? InstrumentId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Fees { get; set; }
    
    // Categorization & metadata
    public Guid? CategoryId { get; set; }
    public string? Merchant { get; set; }
    public bool IsReconciled { get; set; } = false;
    public Guid? RelatedTxnId { get; set; }
    public string? ExternalId { get; set; }
    
    /// <summary>FK to the FinancialConnection that sourced this transaction. Null for manually entered transactions.</summary>
    public Guid? ConnectionId { get; set; }
    
    /// <summary>
    /// The provider's unique transaction identifier (Plaid transaction_id, Basiq transaction id, CSV row hash, etc.).
    /// Used for deduplication across syncs. Mirrors ExternalId but is set on committed transactions (not just staging).
    /// </summary>
    public string? ExternalTransactionId { get; set; }
    public string? ReceiptUrl { get; set; }
    public bool IsTaxDeductible { get; set; } = false;
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    
    // Import & Ledger fields
    public TransactionDirection Direction { get; set; }
    public Guid? TransferGroupId { get; set; }
    public string Source { get; set; } = "MANUAL";
    public Guid? SourceTransactionsFileId { get; set; }
    /// <summary>When true, this repayment is above the minimum — counts toward loan redraw availability.</summary>
    public bool IsExtraRepayment { get; set; } = false;
    
    /// <summary>
    /// Raw FITID from the OFX file, stored verbatim on the committed transaction.
    /// Null for QIF imports or OFX rows where FITID was absent.
    /// </summary>
    public string? FitId { get; set; }
    
    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public Instrument? Instrument { get; set; }
    public Category? Category { get; set; }
}
