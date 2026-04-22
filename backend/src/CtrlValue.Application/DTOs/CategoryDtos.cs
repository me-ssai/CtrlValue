using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Category DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
