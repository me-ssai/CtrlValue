using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class Category : BaseEntity
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
