using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.DTOs;

public class AccountKeywordRuleDto
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string Keyword { get; set; } = string.Empty;
    public string NormalizedKeyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; }
    public bool IsCaseSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAccountKeywordRuleRequest
{
    public Guid AccountId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; } = KeywordMatchType.Contains;
    public bool IsCaseSensitive { get; set; } = false;
}

public class UpdateAccountKeywordRuleRequest
{
    public Guid AccountId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; }
    public bool IsCaseSensitive { get; set; }
}
