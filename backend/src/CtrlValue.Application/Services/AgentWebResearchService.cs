using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentWebResearchService : IAgentWebResearchService
{
    private readonly AppDbContext _db;
    private readonly IModelProviderFactory _providerFactory;
    private readonly IAgentSettingService _settings;
    private readonly IEntityIntegrationService _integrations;
    private readonly ILogger<AgentWebResearchService> _logger;

    // Predefined macro topics with queries and TTLs
    private static readonly Dictionary<string, (string DisplayName, string Query, TimeSpan Ttl)> Topics = new()
    {
        ["au_interest_rates"]  = ("Interest Rates (AU)",   "Current RBA cash rate and interest rate outlook Australia 2025", TimeSpan.FromHours(4)),
        ["cpi_inflation_au"]   = ("Inflation (AU)",        "Current CPI inflation rate Australia 2025 trend", TimeSpan.FromHours(12)),
        ["recession_risk_au"]  = ("Recession Risk (AU)",   "Australia recession risk and economic outlook 2025", TimeSpan.FromHours(24)),
        ["geopolitical_risk"]  = ("Geopolitical Risk",     "Current global geopolitical risks affecting financial markets 2025", TimeSpan.FromHours(24)),
        ["commodities_au"]     = ("Commodities",           "Gold oil and commodities prices outlook 2025", TimeSpan.FromHours(12)),
        ["housing_au"]         = ("Housing Market (AU)",   "Australian housing market outlook 2025 prices and trends", TimeSpan.FromHours(24)),
        ["equities_sentiment"] = ("Equities Sentiment",    "Global equities market sentiment and outlook 2025", TimeSpan.FromHours(12)),
    };

    public AgentWebResearchService(
        AppDbContext db,
        IModelProviderFactory providerFactory,
        IAgentSettingService settings,
        IEntityIntegrationService integrations,
        ILogger<AgentWebResearchService> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _settings = settings;
        _integrations = integrations;
        _logger = logger;
    }

    public async Task<MacroSummaryDto> GetMacroSummaryAsync(string topicKey, Guid entityId, CancellationToken ct = default)
    {
        if (!Topics.TryGetValue(topicKey, out var topic))
            throw new ArgumentException($"Unknown macro topic key: '{topicKey}'");

        // Check cache
        var cached = await _db.AgentWebResearchCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TopicKey == topicKey, ct);

        if (cached != null && cached.ValidUntil > DateTime.UtcNow)
        {
            _logger.LogDebug("[MacroResearch] Cache hit for topic '{Topic}'", topicKey);
            return MapToDto(cached, topic.DisplayName, isCached: true);
        }

        return await FetchAndCacheAsync(topicKey, topic, entityId, ct);
    }

    public async Task<List<MacroSummaryDto>> GetAllMacroSummariesAsync(Guid entityId, CancellationToken ct = default)
    {
        var results = new List<MacroSummaryDto>();

        foreach (var topicKey in Topics.Keys)
        {
            try
            {
                var summary = await GetMacroSummaryAsync(topicKey, entityId, ct);
                results.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MacroResearch] Failed to fetch topic '{Topic}'", topicKey);
            }
        }

        return results;
    }

    private async Task<MacroSummaryDto> FetchAndCacheAsync(
        string topicKey,
        (string DisplayName, string Query, TimeSpan Ttl) topic,
        Guid entityId,
        CancellationToken ct)
    {
        _logger.LogInformation("[MacroResearch] Fetching fresh summary for topic '{Topic}'", topicKey);

        // Resolve provider using the same 3-tier logic as AgentOrchestratorService
        var selectedProviderName = await _settings.GetAsync("DefaultProvider");
        var provider = selectedProviderName != null
            ? _providerFactory.GetProvider(selectedProviderName)
            : _providerFactory.GetDefaultProvider();

        var integrationKey = provider.ProviderName.ToUpperInvariant() == "ANTHROPIC" ? "ANTHROPIC" : "OPENAI";
        var apiKey = await _integrations.GetEffectiveApiKeyAsync(entityId, integrationKey);

        var messages = new List<ModelMessage>
        {
            new("user", AgentPromptService.BuildMacroResearchPrompt(topic.Query))
        };

        // Only enable web search if the resolved provider supports it
        var response = await provider.GenerateResponseAsync(
            messages,
            enableWebSearch: provider.SupportsWebSearch,
            maxTokens: 512,
            apiKeyOverride: apiKey,
            ct: ct);

        var now = DateTime.UtcNow;
        var validUntil = now.Add(topic.Ttl);

        var sourcesJson = response.Sources != null
            ? System.Text.Json.JsonSerializer.Serialize(response.Sources)
            : null;

        // Upsert cache entry
        var existing = await _db.AgentWebResearchCaches
            .FirstOrDefaultAsync(c => c.TopicKey == topicKey, ct);

        if (existing != null)
        {
            existing.Query = topic.Query;
            existing.Summary = response.Content;
            existing.Sources = sourcesJson;
            existing.ValidUntil = validUntil;
            existing.ProviderModel = provider.DefaultChatModel;
            existing.UpdatedAt = now;
        }
        else
        {
            _db.AgentWebResearchCaches.Add(new AgentWebResearchCache
            {
                TopicKey = topicKey,
                Query = topic.Query,
                Summary = response.Content,
                Sources = sourcesJson,
                ValidUntil = validUntil,
                ProviderModel = provider.DefaultChatModel
            });
        }

        await _db.SaveChangesAsync(ct);

        return new MacroSummaryDto
        {
            TopicKey = topicKey,
            TopicDisplayName = topic.DisplayName,
            Summary = response.Content,
            Sources = sourcesJson,
            ValidUntil = validUntil,
            FetchedAt = now,
            IsCached = false,
            ProviderModel = provider.DefaultChatModel
        };
    }

    private static MacroSummaryDto MapToDto(AgentWebResearchCache c, string displayName, bool isCached) => new()
    {
        TopicKey = c.TopicKey,
        TopicDisplayName = displayName,
        Summary = c.Summary,
        Sources = c.Sources,
        ValidUntil = c.ValidUntil,
        FetchedAt = c.UpdatedAt ?? c.CreatedAt,
        IsCached = isCached,
        ProviderModel = c.ProviderModel
    };
}
