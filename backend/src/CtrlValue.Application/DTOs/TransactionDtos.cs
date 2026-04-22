using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Transaction DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime TxnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TxnType { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public string Direction { get; set; } = string.Empty;
    public Guid? CounterAccountId { get; set; }
    public string? CounterAccountName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    public Guid? InstrumentId { get; set; }
    public string? InstrumentSymbol { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Fees { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Merchant { get; set; }
    public bool IsReconciled { get; set; }
    public bool IsTaxDeductible { get; set; }
    public string? Tags { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? ExternalId { get; set; }
    public Guid? RelatedTxnId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTransactionRequest
{
    public DateTime TxnTime { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public TransactionType TxnType { get; set; }
    public Guid AccountId { get; set; }
    public TransactionDirection Direction { get; set; }
    public Guid? CounterAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    public Guid? InstrumentId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Fees { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Merchant { get; set; }
    public bool IsTaxDeductible { get; set; }
    public string? Tags { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? ExternalId { get; set; }
    public Guid? RelatedTxnId { get; set; }
}

public class UpdateTransactionRequest
{
    public DateTime TxnTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType TxnType { get; set; }
    public Guid AccountId { get; set; }
    public TransactionDirection Direction { get; set; }
    public Guid? CounterAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "AUD";
    public Guid? InstrumentId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Fees { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Merchant { get; set; }
    public bool IsTaxDeductible { get; set; }
    public string? Tags { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? ExternalId { get; set; }
    public Guid? RelatedTxnId { get; set; }
}

public class BulkDeleteTransactionsRequest
{
    public List<Guid> TransactionIds { get; set; } = new();
}
