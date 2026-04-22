namespace CtrlValue.Domain.Entities;

public class Entity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "AUD";
    public string Country { get; set; } = "AU"; // ISO 3166-1 alpha-2
    public bool IsDemo { get; set; } = false;

    // Navigation properties
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<EntityUser> EntityUsers { get; set; } = new List<EntityUser>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public ICollection<EntityCustomRole> CustomRoles { get; set; } = new List<EntityCustomRole>();
    public ICollection<EntityIntegration> Integrations { get; set; } = new List<EntityIntegration>();
    public ICollection<FinancialConnection> Connections { get; set; } = new List<FinancialConnection>();
}
