namespace CtrlValue.Application.DTOs;

// ═══════════════════════════════════════════════════════════════════════════════
// Finance Context — the structured snapshot passed to every LLM call
// ═══════════════════════════════════════════════════════════════════════════════

public class FinanceContextDto
{
    public Guid EntityId { get; set; }
    public DateTime AsOf { get; set; }
    public string Currency { get; set; } = "AUD";
    public NetWorthSummaryDto NetWorth { get; set; } = new();
    public CashPositionDto Cash { get; set; } = new();
    public SpendingBehaviourDto Spending { get; set; } = new();
    public SavingBehaviourDto Saving { get; set; } = new();
    public LiabilityPositionDto Liabilities { get; set; } = new();
    public InvestmentPositionDto Investments { get; set; } = new();
    public AssetEfficiencyDto Assets { get; set; } = new();
}

public class NetWorthSummaryDto
{
    public decimal Total { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    /// <summary>Asset value broken down by asset class name (e.g. "CASH", "PROPERTY", "STOCK").</summary>
    public Dictionary<string, decimal> ByAssetClass { get; set; } = new();
}

public class CashPositionDto
{
    public decimal TotalCashBalance { get; set; }
    /// <summary>Cash above a 3-month expense buffer — considered idle.</summary>
    public decimal EstimatedIdleCash { get; set; }
    /// <summary>Estimated 3-month emergency fund requirement (3 × avg monthly expenses).</summary>
    public decimal EmergencyFundEstimate { get; set; }
    public decimal MonthlyCashSurplusDeficit { get; set; }
    public List<ContextAccountItemDto> CashAccounts { get; set; } = new();
}

public class ContextAccountItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public decimal Balance { get; set; }
    public string AssetClass { get; set; } = string.Empty;
}

public class SpendingBehaviourDto
{
    public decimal MonthlyAverageTotal { get; set; }
    /// <summary>Estimated essential spend (utilities, groceries, housing, insurance, transport).</summary>
    public decimal EssentialEstimate { get; set; }
    public decimal DiscretionaryEstimate { get; set; }
    public List<CategorySpendDto> TopCategories { get; set; } = new();
    public List<SubscriptionDto> Subscriptions { get; set; } = new();
    public decimal MonthlySubscriptionTotal { get; set; }
    /// <summary>"Up", "Down", or "Stable" over the last 3 months.</summary>
    public string TrendDirection { get; set; } = "Stable";
    /// <summary>
    /// Spending growth % comparing the most recent 3 months vs. the prior 3 months.
    /// Positive = spending increased; negative = spending decreased. Null if insufficient data.
    /// </summary>
    public decimal? SpendingGrowthPercent { get; set; }
}

public class CategorySpendDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal MonthlyAverage { get; set; }
    public decimal Last30Days { get; set; }
    public decimal PercentOfTotal { get; set; }
}

public class SubscriptionDto
{
    public string MerchantName { get; set; } = string.Empty;
    public decimal TypicalAmount { get; set; }
    public string Cadence { get; set; } = string.Empty;
}

public class SavingBehaviourDto
{
    public decimal AverageMonthlyIncome { get; set; }
    public decimal AverageMonthlyExpenses { get; set; }
    public decimal AverageMonthlySavings { get; set; }
    public decimal SavingsRatePercent { get; set; }
    /// <summary>"Consistent", "Irregular", "Declining", or "Negative".</summary>
    public string Consistency { get; set; } = "Unknown";
    public int MonthsAnalysed { get; set; }
}

public class LiabilityPositionDto
{
    public decimal TotalDebt { get; set; }
    public decimal TotalMonthlyRepayments { get; set; }
    /// <summary>Monthly repayments / average monthly income.</summary>
    public decimal DebtToIncomeRatio { get; set; }
    public List<ContextLiabilityItemDto> Liabilities { get; set; } = new();
}

public class ContextLiabilityItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public decimal Balance { get; set; }
    public decimal? MonthlyRepayment { get; set; }
    public decimal? InterestRate { get; set; }
    public bool HasLoanDetails { get; set; }
}

public class InvestmentPositionDto
{
    public decimal TotalValue { get; set; }
    public Dictionary<string, decimal> ByAssetClass { get; set; } = new();
    /// <summary>True if any single asset class exceeds 40% of total investment value.</summary>
    public bool HasConcentrationRisk { get; set; }
    /// <summary>E.g. "PROPERTY (67%)" — the dominant concentration if HasConcentrationRisk is true.</summary>
    public string? LargestConcentration { get; set; }
}

public class AssetEfficiencyDto
{
    public decimal IncomeProducingAssetValue { get; set; }
    public decimal CostGeneratingAssetValue { get; set; }
    public int VehicleCount { get; set; }
    public List<string> CostGeneratingAssetNames { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// Feature Flags
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentFeatureFlagDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string SectionKey { get; set; } = string.Empty;
}

public class UpdateFeatureFlagRequest
{
    public bool IsEnabled { get; set; }
}

public class UserFeatureFlagOverrideDto
{
    public Guid AssignmentId { get; set; }
    public string FlagKey { get; set; } = string.Empty;
    public string FlagName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class AgentConfigDto
{
    public bool AgentEnabled { get; set; }
    public Dictionary<string, bool> SectionFlags { get; set; } = new();
    public string? ActiveProvider { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Conversations + Messages
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class CreateConversationRequest
{
    public string? Title { get; set; }
    public string SectionType { get; set; } = "Chat";
}

public class AgentMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? StructuredPayload { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    /// <summary>"internal", "web", or "hybrid" — drives the source badge in the UI.</summary>
    public string? SourceType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    /// <summary>Continue an existing conversation. If null, a new conversation is created.</summary>
    public Guid? ConversationId { get; set; }
}

public class AgentChatResponse
{
    public Guid ConversationId { get; set; }
    public AgentMessageDto UserMessage { get; set; } = null!;
    public AgentMessageDto AssistantMessage { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Insights
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentInsightDto
{
    public Guid Id { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public bool IsDismissed { get; set; }
    public DateTime GeneratedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Macro / Web Research
// ═══════════════════════════════════════════════════════════════════════════════

public class MacroSummaryDto
{
    public string TopicKey { get; set; } = string.Empty;
    public string TopicDisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Sources { get; set; }
    public DateTime ValidUntil { get; set; }
    public DateTime FetchedAt { get; set; }
    public bool IsCached { get; set; }
    public string? ProviderModel { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Savings History
// ═══════════════════════════════════════════════════════════════════════════════

public class SavingsSnapshotDto
{
    public Guid Id { get; set; }
    public DateTime AsOfDate { get; set; }
    public decimal SavingsRatePercent { get; set; }
    public decimal AverageMonthlyIncome { get; set; }
    public decimal AverageMonthlyExpenses { get; set; }
    public decimal AverageMonthlySavings { get; set; }
    public string Currency { get; set; } = "AUD";
}

// ═══════════════════════════════════════════════════════════════════════════════
// Scenarios
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Scenario types supported by the scenario engine.
/// - CutCategory:          Reduce spending in a category by X% per month
/// - PayOffLoan:           Pay off a specific loan early
/// - IncreaseSavingsRate:  Target a higher savings rate and project net worth
/// - SellVehicle:          Sell one of the tracked vehicles
/// </summary>
public class RunScenarioRequest
{
    /// <summary>One of: CutCategory, PayOffLoan, IncreaseSavingsRate, SellVehicle</summary>
    public string ScenarioType { get; set; } = string.Empty;

    // CutCategory
    public string? CategoryName { get; set; }
    public decimal? ReductionPercent { get; set; }

    // PayOffLoan
    public string? LoanName { get; set; }

    // IncreaseSavingsRate
    public decimal? TargetSavingsRatePercent { get; set; }
    public int? ProjectionYears { get; set; }

    // SellVehicle
    public string? VehicleName { get; set; }
}

public class ScenarioResultDto
{
    public string ScenarioType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<ScenarioMetricDto> Metrics { get; set; } = new();
    public string? Disclaimer { get; set; }
}

public class ScenarioMetricDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool IsHighlight { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Audit Log
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentAuditLogDto
{
    public Guid Id { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SafetyDecision { get; set; }
    public int? TotalTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Scenario History
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentScenarioHistoryDto
{
    public Guid Id { get; set; }
    public string ScenarioType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ScenarioResultDto Result { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Agent Settings
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UpdateAgentSettingRequest
{
    public string Value { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Weekly Digest
// ═══════════════════════════════════════════════════════════════════════════════

public class AgentDigestEmailDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EntityId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WeekKey { get; set; } = string.Empty;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
