using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

public class CategoryKeywordRuleDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string NormalizedKeyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; }
    public bool IsCaseSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateCategoryKeywordRuleRequest
{
    public Guid CategoryId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; } = KeywordMatchType.Contains;
    public bool IsCaseSensitive { get; set; } = false;
}

public class UpdateCategoryKeywordRuleRequest
{
    public Guid CategoryId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; }
    public bool IsCaseSensitive { get; set; }
}

public class ApplyRulesResultDto
{
    public int CategorizedCount { get; set; }
}

public class CategorySuggestionDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string MatchedKeyword { get; set; } = string.Empty;
}
