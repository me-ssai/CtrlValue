namespace CtrlValue.Application.Interfaces;

/// <summary>
/// Abstraction over LLM providers.
/// Phase 1: OpenAI only. Phase 4: Claude (Anthropic) added.
/// </summary>
public interface IModelProvider
{
    string ProviderName { get; }
    string DefaultChatModel { get; }
    bool SupportsWebSearch { get; }

    /// <summary>
    /// Sends a conversation to the model and returns the assistant's response.
    /// </summary>
    /// <param name="apiKeyOverride">
    /// If supplied, overrides the provider's configured API key for this call only.
    /// Used for per-entity key resolution via EntityIntegrationService.
    /// </param>
    Task<ModelResponse> GenerateResponseAsync(
        List<ModelMessage> messages,
        string? model = null,
        bool enableWebSearch = false,
        int? maxTokens = null,
        string? apiKeyOverride = null,
        CancellationToken ct = default);
}

public record ModelMessage(string Role, string Content);

public record ModelResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    /// <summary>"internal", "web", or "hybrid"</summary>
    string? SourceType,
    List<string>? ToolsUsed,
    List<ModelSource>? Sources
);

public record ModelSource(string Title, string? Url);
