using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Application.Services;

/// <summary>
/// Anthropic Claude provider. Implements IModelProvider.
/// Uses Anthropic's Messages API (/v1/messages).
/// Registered via AddHttpClient in Program.cs.
/// Does not support native web_search_preview (OpenAI-specific).
/// </summary>
public class ClaudeProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<ClaudeProvider> _logger;
    private readonly string _chatModel;

    public string ProviderName => "Anthropic";
    public string DefaultChatModel => _chatModel;
    public bool SupportsWebSearch => false;

    public ClaudeProvider(HttpClient http, IConfiguration config, ILogger<ClaudeProvider> logger)
    {
        _http = http;
        _logger = logger;
        _chatModel = config["Agent:Anthropic:ChatModel"] ?? "claude-sonnet-4-6";
    }

    public async Task<ModelResponse> GenerateResponseAsync(
        List<ModelMessage> messages,
        string? model = null,
        bool enableWebSearch = false,
        int? maxTokens = null,
        string? apiKeyOverride = null,
        CancellationToken ct = default)
    {
        var resolvedModel = model ?? _chatModel;

        // Anthropic Messages API separates system prompt from conversation messages
        var systemPrompt = messages
            .Where(m => m.Role == "system")
            .Select(m => m.Content)
            .FirstOrDefault() ?? string.Empty;

        var conversationMessages = messages
            .Where(m => m.Role != "system")
            .Select(m => new { role = m.Role, content = m.Content })
            .ToList();

        var requestBody = new
        {
            model = resolvedModel,
            max_tokens = maxTokens ?? 2048,
            system = systemPrompt,
            messages = conversationMessages
        };

        HttpResponseMessage response;
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages");
            request.Headers.Add("x-api-key", apiKeyOverride);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = JsonContent.Create(requestBody);
            response = await _http.SendAsync(request, ct);
        }
        else
        {
            response = await _http.PostAsJsonAsync("/v1/messages", requestBody, ct);
        }

        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var root = doc.RootElement;

        // Anthropic response: content[0].text
        var content = root.TryGetProperty("content", out var contentArr)
            && contentArr.GetArrayLength() > 0
            && contentArr[0].TryGetProperty("text", out var textEl)
            ? textEl.GetString() ?? string.Empty
            : string.Empty;

        // Usage
        int inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("usage", out var usageEl))
        {
            if (usageEl.TryGetProperty("input_tokens", out var inp))  inputTokens  = inp.GetInt32();
            if (usageEl.TryGetProperty("output_tokens", out var out_)) outputTokens = out_.GetInt32();
        }

        _logger.LogDebug(
            "[ClaudeProvider] Model={Model} Tokens={Input}+{Output}",
            resolvedModel, inputTokens, outputTokens);

        return new ModelResponse(
            Content: content,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            SourceType: "internal",
            ToolsUsed: null,
            Sources: null);
    }
}
