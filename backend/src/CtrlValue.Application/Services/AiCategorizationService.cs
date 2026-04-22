using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;

namespace CtrlValue.Application.Services;

public class AiCategorizationService : IAiCategorizationService
{
    // ────────────────────────────────────────────────────────────────────────────
    // System prompt
    // ────────────────────────────────────────────────────────────────────────────

    private const string SystemPrompt = """
        You are a financial transaction categorisation assistant.

        You will receive a JSON object with two fields:
          - "transactions": an array of transactions to categorise
          - "categories": the available categories for this account holder

        For each transaction, select the single best-matching category from the
        provided list based on the description, direction (Inflow/Outflow), and amount.
        Only choose categories whose "type" matches the transaction direction
        (Expense for Outflow, Income for Inflow) unless the transaction is clearly
        a transfer or has no obvious match — in that case use use Uncategorised Expense or Uncategorised Income. In rare cases, use null.

        You must to categories all of them to the best possible match. User can always correct it at later stage.

        If no category is a reasonable match, use Uncategorised Expense or Uncategorised Income based on the inflow or outflow of money for categoryId.

        Respond with ONLY a valid JSON array — no markdown, no explanation, no code fences:
        [
          { "id": "<transaction id>", "categoryId": "<category guid or null>", "categoryName": "<name or null>", "confidence": "high|medium|low" },
          ...
        ]
        """;

    // ────────────────────────────────────────────────────────────────────────────
    // Fields / constructor
    // ────────────────────────────────────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly ILogger<AiCategorizationService> _logger;
    private readonly string _model;
    private readonly int _batchSize;

    private static readonly JsonSerializerOptions CaseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    public AiCategorizationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AiCategorizationService> logger)
    {
        _http  = httpClient;
        _logger = logger;
        _model     = configuration["ProjectZAI:Model"]     ?? "local-model";
        _batchSize = int.TryParse(configuration["ProjectZAI:BatchSize"], out var b) ? b : 25;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Public API
    // ────────────────────────────────────────────────────────────────────────────

    public async Task CategorizeAsync(
        IReadOnlyList<ImportedTransactionsFileStaging> rows,
        IReadOnlyList<Category> availableCategories,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;

        // Build the category lookup once (used both for the prompt and result validation)
        var categoryIds = availableCategories
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToHashSet();

        var categoryOptions = availableCategories
            .Where(c => c.IsActive)
            .Select(c => new AiCategoryOption
            {
                Id         = c.Id.ToString(),
                Name       = c.Name,
                Type       = c.CategoryType.ToString(),
                ParentName = c.ParentCategory?.Name
            })
            .ToList();

        if (categoryOptions.Count == 0)
        {
            _logger.LogInformation(
                "[AI Categorisation] No active categories available — skipping.");
            return;
        }

        // Process rows in batches
        int totalRows      = rows.Count;
        int categorised    = 0;
        int batchCount     = 0;

        for (int offset = 0; offset < totalRows; offset += _batchSize)
        {
            var batch = rows.Skip(offset).Take(_batchSize).ToList();
            batchCount++;

            try
            {
                var results = await CallAiAsync(batch, categoryOptions, cancellationToken);
                int applied = ApplyResults(batch, results, categoryIds);
                categorised += applied;

                _logger.LogInformation(
                    "[AI Categorisation] Batch {Batch}: {Applied}/{BatchSize} rows categorised.",
                    batchCount, applied, batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[AI Categorisation] Batch {Batch} failed (offset {Offset}). " +
                    "Rows in this batch will remain uncategorised.",
                    batchCount, offset);
                // Best-effort: continue to next batch
            }
        }

        _logger.LogInformation(
            "[AI Categorisation] Complete — {Categorised}/{Total} rows assigned a category across {Batches} batch(es).",
            categorised, totalRows, batchCount);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // AI HTTP call
    // ────────────────────────────────────────────────────────────────────────────

    private async Task<List<AiCategorizationResult>> CallAiAsync(
        List<ImportedTransactionsFileStaging> batch,
        List<AiCategoryOption> categories,
        CancellationToken cancellationToken)
    {
        var transactions = batch.Select(r => new AiTransactionPayload
        {
            Id          = r.Id.ToString(),
            Date        = r.TransactionDate.ToString("yyyy-MM-dd"),
            Amount      = r.Amount,
            Direction   = r.AmountRaw > 0 ? "Inflow" : "Outflow",
            Description = r.Description,
            Notes       = r.Notes
        }).ToList();

        var userContent = JsonSerializer.Serialize(
            new { transactions, categories },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var requestBody = new
        {
            model    = _model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = userContent }
            },
            temperature = 0.0,
            max_tokens  = 4096
        };

        var response = await _http.PostAsJsonAsync("/v1/chat/completions", requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Parse the OpenAI-compatible response envelope
        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "[]";

        // Strip potential markdown code fences the model might emit despite instructions
        content = StripMarkdownFences(content.Trim());

        return JsonSerializer.Deserialize<List<AiCategorizationResult>>(content, CaseInsensitive)
               ?? new List<AiCategorizationResult>();
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Apply results back to staging rows
    // ────────────────────────────────────────────────────────────────────────────

    private static int ApplyResults(
        List<ImportedTransactionsFileStaging> batch,
        List<AiCategorizationResult> results,
        HashSet<Guid> validCategoryIds)
    {
        // Build a quick lookup: rowId string → categoryId string
        var lookup = results
            .Where(r => !string.IsNullOrWhiteSpace(r.CategoryId))
            .ToDictionary(r => r.Id, r => r.CategoryId!);

        int applied = 0;

        foreach (var row in batch)
        {
            var rowIdStr = row.Id.ToString();

            if (!lookup.TryGetValue(rowIdStr, out var categoryIdStr))
                continue;

            if (!Guid.TryParse(categoryIdStr, out var categoryGuid))
                continue;

            // Guard: only apply if the category actually belongs to this tenant
            if (!validCategoryIds.Contains(categoryGuid))
                continue;

            row.CategoryId = categoryGuid;
            applied++;
        }

        return applied;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes ```json ... ``` or ``` ... ``` fences that some models emit
    /// despite being instructed not to.
    /// </summary>
    private static string StripMarkdownFences(string content)
    {
        if (content.StartsWith("```"))
        {
            // Remove opening fence (potentially ```json)
            var firstNewline = content.IndexOf('\n');
            if (firstNewline >= 0)
                content = content[(firstNewline + 1)..];

            // Remove closing fence
            if (content.EndsWith("```"))
                content = content[..^3].TrimEnd();
        }

        return content.Trim();
    }
}
