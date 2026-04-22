using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetCategoriesAsync(Guid entityId, CategoryType? type = null);
    Task<CategoryDto> GetCategoryByIdAsync(Guid id, Guid entityId);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, Guid entityId);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, Guid entityId);
    Task DeleteCategoryAsync(Guid id, Guid entityId);
}
