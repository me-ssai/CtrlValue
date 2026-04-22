namespace CtrlValue.Application.DTOs;

public class DashboardSummary
{
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal NetWorth { get; set; }
    public int AssetCount { get; set; }
    public int LiabilityCount { get; set; }
    public int TransactionCountThisMonth { get; set; }
    public decimal IncomeThisMonth { get; set; }
    public decimal ExpensesThisMonth { get; set; }
    public List<RecentTransaction> RecentTransactions { get; set; } = new();
    public List<AccountHoldingDto> Holdings { get; set; } = new();
}

public class RecentTransaction
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}
