using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Account DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AssetClass { get; set; }
    public string? LiquidityClass { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public decimal? CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal StartingBalance { get; set; }
    public DateTime StartingBalanceDate { get; set; }
    public bool IsOffsetAccount { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ExternalId { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AssetClass? AssetClass { get; set; }
    public LiquidityClass? LiquidityClass { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
    public decimal? CreditLimit { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>Known balance as of <see cref="StartingBalanceDate"/>. Defaults to 0.</summary>
    public decimal StartingBalance { get; set; } = 0;
    /// <summary>Cut-off date for balance recalculation. Defaults to today (server UTC).</summary>
    public DateTime? StartingBalanceDate { get; set; }
    public bool IsOffsetAccount { get; set; } = false;
    public string? ExternalId { get; set; }
}

public class UpdateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public AssetClass? AssetClass { get; set; }
    public LiquidityClass? LiquidityClass { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? CreditLimit { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>Update the starting balance anchor. Also updates the OpeningBalance transaction.</summary>
    public decimal StartingBalance { get; set; } = 0;
    public DateTime? StartingBalanceDate { get; set; }
    public bool IsOffsetAccount { get; set; } = false;
    public string? ExternalId { get; set; }
}

public class AccountSummaryDto
{
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal NetWorth { get; set; }
    public string Currency { get; set; } = "AUD";
    public int AssetCount { get; set; }
    public int LiabilityCount { get; set; }
    public List<AccountHoldingDto> Holdings { get; set; } = new();
}

public class AccountHoldingDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? AssetClass { get; set; }
    public string? LiquidityClass { get; set; }
    public decimal Value { get; set; }
}

public class AccountDeletionImpactDto
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public int ValuationCount { get; set; }
    public int PositionCount { get; set; }
    public bool HasDepreciationSchedule { get; set; }
}
