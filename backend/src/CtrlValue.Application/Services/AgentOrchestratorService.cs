using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly AppDbContext _db;
    private readonly IAgentFeatureFlagService _flags;
    private readonly IAgentContextBuilderService _contextBuilder;
    private readonly IModelProviderFactory _providerFactory;
    private readonly IEntityIntegrationService _integrations;
    private readonly IAgentSettingService _settings;
    private readonly IAgentAuditService _audit;
    private readonly ILogger<AgentOrchestratorService> _logger;

    private const int MaxHistoryMessages = 10;

    public AgentOrchestratorService(
        AppDbContext db,
        IAgentFeatureFlagService flags,
        IAgentContextBuilderService contextBuilder,
        IModelProviderFactory providerFactory,
        IEntityIntegrationService integrations,
        IAgentSettingService settings,
        IAgentAuditService audit,
        ILogger<AgentOrchestratorService> logger)
    {
        _db = db;
        _flags = flags;
        _contextBuilder = contextBuilder;
        _providerFactory = providerFactory;
        _integrations = integrations;
        _settings = settings;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ChatAsync(
        Guid userId,
        Guid entityId,
        SendMessageRequest request,
        CancellationToken ct = default)
    {
        if (!await _flags.IsSectionAccessibleAsync(userId, AgentSectionKey.ConversationalChat))
            throw new UnauthorizedAccessException("Chat section is not enabled for this user.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Message content cannot be empty.");

        // Build finance context
        var ctx = await _contextBuilder.BuildContextAsync(userId, entityId, forceRefresh: false, ct);

        // Resolve provider — check DB setting first, fall back to factory default
        var selectedProviderName = await _settings.GetAsync("DefaultProvider");
        var provider = selectedProviderName != null
            ? _providerFactory.GetProvider(selectedProviderName)
            : _providerFactory.GetDefaultProvider();

        // Resolve API key for the selected provider
        var integrationKey = provider.ProviderName.ToUpperInvariant() switch
        {
            "ANTHROPIC" => "ANTHROPIC",
            _           => "OPENAI"
        };
        var providerApiKey = await _integrations.GetEffectiveApiKeyAsync(entityId, integrationKey);

        // Resolve or create conversation
        AgentConversation conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await _db.AgentConversations
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value
                    && c.UserId == userId && c.EntityId == entityId, ct)
                ?? throw new KeyNotFoundException("Conversation not found.");
        }
        else
        {
            // Auto-title from first ~60 chars of message
            var title = request.Content.Length > 60
                ? request.Content[..60] + "…"
                : request.Content;

            var providerEnum = provider.ProviderName == "Anthropic"
                ? AgentProviderName.Anthropic
                : AgentProviderName.OpenAI;

            conversation = new AgentConversation
            {
                UserId = userId,
                EntityId = entityId,
                Title = title,
                Provider = providerEnum,
                ModelName = provider.DefaultChatModel,
                SectionType = AgentConversationSectionType.Chat
            };
            _db.AgentConversations.Add(conversation);
            await _db.SaveChangesAsync(ct);
        }

        // Build message history (last N messages to stay within token budget)
        var history = await _db.AgentMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .ToListAsync(ct);

        history.Reverse(); // chronological order

        var messages = new List<ModelMessage>
        {
            new("system", AgentPromptService.BuildChatSystemPrompt(ctx))
        };

        foreach (var h in history)
            messages.Add(new ModelMessage(h.Role.ToString().ToLower(), h.Content));

        messages.Add(new ModelMessage("user", request.Content));

        // Call the model — pass entity API key override if resolved
        var response = await provider.GenerateResponseAsync(
            messages,
            model: provider.DefaultChatModel,
            enableWebSearch: false,
            maxTokens: 1024,
            apiKeyOverride: providerApiKey,
            ct: ct);

        var now = DateTime.UtcNow;

        // Persist user message
        var userMessage = new AgentMessage
        {
            ConversationId = conversation.Id,
            Role = AgentMessageRole.User,
            Content = request.Content,
            SourceType = "internal"
        };
        _db.AgentMessages.Add(userMessage);

        // Persist assistant message
        var assistantMessage = new AgentMessage
        {
            ConversationId = conversation.Id,
            Role = AgentMessageRole.Assistant,
            Content = response.Content,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            ToolCalls = response.ToolsUsed != null
                ? JsonSerializer.Serialize(response.ToolsUsed) : null,
            SourceType = response.SourceType
        };
        _db.AgentMessages.Add(assistantMessage);

        conversation.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        // Audit log (fire-and-forget safe)
        await _audit.RecordAsync(new AgentAuditLog
        {
            ConversationId = conversation.Id,
            UserId = userId,
            EntityId = entityId,
            RequestType = "chat",
            Provider = provider.ProviderName,
            Model = provider.DefaultChatModel,
            PromptTemplateVersion = AgentPromptService.PromptTemplateVersion,
            ToolsUsed = response.ToolsUsed != null
                ? JsonSerializer.Serialize(response.ToolsUsed) : null,
            SourcesUsed = response.Sources != null
                ? JsonSerializer.Serialize(response.Sources) : null,
            SafetyDecision = "pass",
            TotalTokens = response.InputTokens + response.OutputTokens
        });

        return new AgentChatResponse
        {
            ConversationId = conversation.Id,
            UserMessage = MapMessage(userMessage),
            AssistantMessage = MapMessage(assistantMessage)
        };
    }

    public async Task<List<AgentConversationDto>> GetConversationsAsync(Guid userId, Guid entityId)
    {
        var conversations = await _db.AgentConversations
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.EntityId == entityId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new AgentConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                Provider = c.Provider.ToString(),
                ModelName = c.ModelName,
                SectionType = c.SectionType.ToString(),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = c.Messages.Count(m => !m.IsDeleted)
            })
            .ToListAsync();

        return conversations;
    }

    public async Task<List<AgentMessageDto>> GetMessagesAsync(Guid conversationId, Guid userId)
    {
        // Verify ownership
        var conversation = await _db.AgentConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId)
            ?? throw new KeyNotFoundException("Conversation not found.");

        var messages = await _db.AgentMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return messages.Select(MapMessage).ToList();
    }

    private static AgentMessageDto MapMessage(AgentMessage m) => new()
    {
        Id = m.Id,
        Role = m.Role.ToString().ToLower(),
        Content = m.Content,
        StructuredPayload = m.StructuredPayload,
        InputTokens = m.InputTokens,
        OutputTokens = m.OutputTokens,
        SourceType = m.SourceType,
        CreatedAt = m.CreatedAt
    };
}
