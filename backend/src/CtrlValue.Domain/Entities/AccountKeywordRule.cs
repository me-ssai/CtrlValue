using CtrlValue.Domain.Enums;

namespace CtrlValue.Domain.Entities;

public class AccountKeywordRule : BaseEntity
{
    public Guid EntityId { get; set; }
    public Guid AccountId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string NormalizedKeyword { get; set; } = string.Empty;
    public KeywordMatchType MatchType { get; set; } = KeywordMatchType.Contains;
    public bool IsCaseSensitive { get; set; } = false;
    public Guid? CreatedByUserId { get; set; }

    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
