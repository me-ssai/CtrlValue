namespace CtrlValue.Application.DTOs;

public class DemoConfigDto
{
    public bool IsDemoMode { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
}

public class DemoBootstrapDto
{
    public EntityDto Entity { get; set; } = null!;
    public List<AccountDto> Accounts { get; set; } = [];
    public List<TransactionDto> RecentTransactions { get; set; } = [];
    public List<CategoryDto> Categories { get; set; } = [];
    public List<BudgetDto> Budgets { get; set; } = [];
    public List<PositionDto> Positions { get; set; } = [];
    public List<InstrumentDto> Instruments { get; set; } = [];
}
