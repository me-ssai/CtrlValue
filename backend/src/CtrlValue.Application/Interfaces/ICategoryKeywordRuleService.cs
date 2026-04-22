using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface ICategoryKeywordRuleService
{
    Task<List<CategoryKeywordRuleDto>> GetByCategoryAsync(Guid entityId, Guid categoryId);
    Task<List<CategoryKeywordRuleDto>> GetAllAsync(Guid entityId);
    Task<CategoryKeywordRuleDto?> GetByIdAsync(Guid entityId, Guid ruleId);
    Task<CategoryKeywordRuleDto> CreateAsync(Guid entityId, CreateCategoryKeywordRuleRequest request, Guid? userId = null);
    Task<CategoryKeywordRuleDto> UpdateAsync(Guid entityId, Guid ruleId, UpdateCategoryKeywordRuleRequest request);
    Task DeleteAsync(Guid entityId, Guid ruleId);
    Task<Guid?> MatchCategoryAsync(Guid entityId, string description);
    Task<int> ApplyRulesToWorkspaceAsync(Guid entityId);
    Task<CategorySuggestionDto?> SuggestCategoryAsync(Guid entityId, string description);
}
