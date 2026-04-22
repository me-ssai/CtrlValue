using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(Guid entityId, CategoryType? type = null)
    {
        var query = _db.Categories
            .Include(c => c.ParentCategory)
            .Where(c => c.EntityId == entityId);

        if (type.HasValue)
            query = query.Where(c => c.CategoryType == type.Value);

        return await query
            .OrderBy(c => c.CategoryType)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                CategoryType = c.CategoryType.ToString(),
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                Color = c.Color,
                Icon = c.Icon,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<CategoryDto> GetCategoryByIdAsync(Guid id, Guid entityId)
    {
        var category = await _db.Categories
            .Include(c => c.ParentCategory)
            .Where(c => c.Id == id && c.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (category == null)
            throw new KeyNotFoundException($"Category with ID {id} not found or access denied.");

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CategoryType = category.CategoryType.ToString(),
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.Name,
            Color = category.Color,
            Icon = category.Icon,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Category name is required.");

        var category = new Category
        {
            EntityId = entityId,
            Name = request.Name.Trim(),
            CategoryType = request.CategoryType,
            ParentCategoryId = request.ParentCategoryId,
            Color = request.Color?.Trim(),
            Icon = request.Icon?.Trim(),
            TenantId = "default"
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        // Reload with parent category
        await _db.Entry(category).Reference(c => c.ParentCategory).LoadAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CategoryType = category.CategoryType.ToString(),
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.Name,
            Color = category.Color,
            Icon = category.Icon,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Category name is required.");

        var category = await _db.Categories
            .Include(c => c.ParentCategory)
            .Where(c => c.Id == id && c.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (category == null)
            throw new KeyNotFoundException($"Category with ID {id} not found or access denied.");

        category.Name = request.Name.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.Color = request.Color?.Trim();
        category.Icon = request.Icon?.Trim();
        category.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        // Reload parent category
        await _db.Entry(category).Reference(c => c.ParentCategory).LoadAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CategoryType = category.CategoryType.ToString(),
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.Name,
            Color = category.Color,
            Icon = category.Icon,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };
    }

    public async Task DeleteCategoryAsync(Guid id, Guid entityId)
    {
        var category = await _db.Categories
            .Where(c => c.Id == id && c.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (category == null)
            throw new KeyNotFoundException($"Category with ID {id} not found or access denied.");

        _db.Categories.Remove(category);  // Soft delete via DbContext override
        await _db.SaveChangesAsync();
    }
}
