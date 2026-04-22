using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IAccountKeywordRuleService
{
    Task<List<AccountKeywordRuleDto>> GetAllAsync(Guid entityId);
    Task<AccountKeywordRuleDto?> GetByIdAsync(Guid entityId, Guid ruleId);
    Task<List<AccountKeywordRuleDto>> GetByAccountAsync(Guid entityId, Guid accountId);
    Task<AccountKeywordRuleDto> CreateAsync(Guid entityId, CreateAccountKeywordRuleRequest request, Guid? userId = null);
    Task<AccountKeywordRuleDto> UpdateAsync(Guid entityId, Guid ruleId, UpdateAccountKeywordRuleRequest request);
    Task DeleteAsync(Guid entityId, Guid ruleId);
    Task<Guid?> MatchAccountAsync(Guid entityId, string description);
}
