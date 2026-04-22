namespace CtrlValue.Domain.Entities;

/// <summary>
/// Cached macro web-research results keyed by topic.
/// ValidUntil controls TTL: 4h for interest rates, 12h for inflation, 24h for geopolitics.
/// On fetch, existing row is updated in-place (upsert by TopicKey).
/// </summary>
public class AgentWebResearchCache : BaseEntity
{
    /// <summary>E.g. "au_interest_rates", "cpi_inflation_au", "geopolitical_risk".</summary>
    public string TopicKey { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    /// <summary>Jsonb: array of { title, url, publishedAt } source objects.</summary>
    public string? Sources { get; set; }
    public DateTime ValidUntil { get; set; }
    /// <summary>Which model generated the summary, for audit purposes.</summary>
    public string? ProviderModel { get; set; }
}
