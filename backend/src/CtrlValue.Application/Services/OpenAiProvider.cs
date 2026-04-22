using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.Interfaces;

namespace CtrlValue.Application.Services;

/// <summary>
/// OpenAI Chat Completions provider. Implements IModelProvider.
/// Registered via AddHttpClient in Program.cs.
///
/// Web search note: the Chat Completions API (/v1/chat/completions) uses
/// web_search_options + a search-capable model (e.g. gpt-4o-search-preview).
/// The tools:[{type:"web_search_preview"}] format belongs to the Responses API
/// (/v1/responses) and returns 400 on Chat Completions — do NOT use it here.
/// </summary>
public class OpenAiProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly string _chatModel;
    private readonly string _webSearchModel;

    public string ProviderName => "OpenAI";
    public string DefaultChatModel => _chatModel;
    public bool SupportsWebSearch => true;

    public OpenAiProvider(HttpClient http, IConfiguration config, ILogger<OpenAiProvider> logger)
    {
        _http = http;
        _logger = logger;
        _chatModel = config["Agent:OpenAI:ChatModel"] ?? "gpt-4o";
        // Web search requires a search-capable model. Falls back to gpt-4o-search-preview
        // if not configured. The standard gpt-4o model does not support web_search_options.
        _webSearchModel = config["Agent:OpenAI:WebSearchModel"] ?? "gpt-4o-search-preview";
    }

    public async Task<ModelResponse> GenerateResponseAsync(
        List<ModelMessage> messages,
        string? model = null,
        bool enableWebSearch = false,
        int? maxTokens = null,
        string? apiKeyOverride = null,
        CancellationToken ct = default)
    {
        // When web search is requested use the search-capable model.
        // web_search_options is the correct Chat Completions parameter (not tools:[]).
        var resolvedModel = enableWebSearch
            ? _webSearchModel
            : (model ?? _chatModel);

        var requestMessages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList();

        object requestBody = enableWebSearch
            ? new
            {
                model = resolvedModel,
                messages = requestMessages,
                web_search_options = new { },
                max_tokens = maxTokens ?? 2048
            }
            : new
            {
                model = resolvedModel,
                messages = requestMessages,
                max_tokens = maxTokens ?? 2048
            };

        // If a per-entity API key override is provided, use it for this request only
        HttpResponseMessage response;
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKeyOverride);
            request.Content = JsonContent.Create(requestBody);
            response = await _http.SendAsync(request, ct);
        }
        else
        {
            response = await _http.PostAsJsonAsync("/v1/chat/completions", requestBody, ct);
        }

        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var root = doc.RootElement;

        var content = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        var usage = root.TryGetProperty("usage", out var usageEl) ? usageEl : default;
        int inputTokens = usage.ValueKind != JsonValueKind.Undefined
            ? usage.GetProperty("prompt_tokens").GetInt32() : 0;
        int outputTokens = usage.ValueKind != JsonValueKind.Undefined
            ? usage.GetProperty("completion_tokens").GetInt32() : 0;

        // Extract URL citations from annotations (search-preview models return these)
        var sources = new List<ModelSource>();
        string sourceType = "internal";

        if (enableWebSearch && root.TryGetProperty("choices", out var choices))
        {
            var messageEl = choices[0].GetProperty("message");

            if (messageEl.TryGetProperty("annotations", out var annotations) &&
                annotations.ValueKind == JsonValueKind.Array)
            {
                foreach (var annotation in annotations.EnumerateArray())
                {
                    if (annotation.TryGetProperty("url_citation", out var citation))
                    {
                        var title = citation.TryGetProperty("title", out var t) ? t.GetString() : null;
                        var url   = citation.TryGetProperty("url",   out var u) ? u.GetString() : null;
                        sources.Add(new ModelSource(title ?? url ?? "Source", url));
                    }
                }
            }

            sourceType = sources.Count > 0 ? "web" : "internal";
        }

        _logger.LogDebug(
            "[OpenAiProvider] Model={Model} Tokens={Input}+{Output} Source={Source}",
            resolvedModel, inputTokens, outputTokens, sourceType);

        return new ModelResponse(
            Content: content,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            SourceType: sourceType,
            ToolsUsed: null,
            Sources: sources.Count > 0 ? sources : null);
    }
}
