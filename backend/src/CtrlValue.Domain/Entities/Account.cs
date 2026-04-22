using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Account : BaseEntity
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public AssetClass? AssetClass { get; set; }
    public LiquidityClass? LiquidityClass { get; set; }
    public string Currency { get; set; } = "AUD";
    public string? Institution { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; } = 0;
    /// <summary>The known balance at <see cref="StartingBalanceDate"/>. Acts as the anchor for recalculation.</summary>
    public decimal StartingBalance { get; set; } = 0;
    /// <summary>Transactions on or after this date are included in balance recalculation.</summary>
    public DateTime StartingBalanceDate { get; set; } = DateTime.UtcNow;
    public bool IsOffsetAccount { get; set; } = false;
    public string? ExternalId { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>When set, indicates this account is linked to and synced from the given provider.</summary>
    public FinancialConnectionProvider? ConnectionProvider { get; set; }
    /// <summary>When true, balance and transactions are synced from a financial provider automatically.</summary>
    public bool IsSyncEnabled { get; set; } = false;

    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Valuation> Valuations { get; set; } = new List<Valuation>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public DepreciationSchedule? DepreciationSchedule { get; set; }
    /// <summary>Present when this is a LIABILITY account configured as a loan.</summary>
    public LoanDetails? LoanDetails { get; set; }
    /// <summary>Present when this is an ASSET/PROPERTY account linked to a real estate property.</summary>
    public Property? Property { get; set; }
}
