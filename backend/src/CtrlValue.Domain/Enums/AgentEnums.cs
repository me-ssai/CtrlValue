namespace CtrlValue.Domain.Enums;

public enum AgentSectionKey
{
    AgentCore,
    PersonalFinance,
    MacroInsights,
    NetWorthAnalysis,
    LiabilityReview,
    ConversationalChat,
    ScenarioExploration,
    AlertsNudges,
    ExplanationMode
}

public enum AgentConversationSectionType
{
    Chat,
    MacroInsights,
    NetWorthAnalysis,
    ScenarioExploration
}

public enum AgentInsightType
{
    IdleCash,
    LowSavingsRate,
    SubscriptionCreep,
    HighSpendCategory,
    LiabilityDrag,
    ConcentrationRisk,
    NonIncomeAsset,
    LifestyleCreep,
    // Section E — Liability & Asset Efficiency
    HighInterestDebt,
    MultipleVehicles
}

public enum AgentInsightSeverity
{
    Info,
    Warning,
    Alert
}

public enum AgentInsightSourceType
{
    Internal,
    Web,
    Hybrid
}

public enum AgentMessageRole
{
    User,
    Assistant,
    System
}

public enum AgentProviderName
{
    OpenAI,
    Anthropic
}
