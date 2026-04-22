using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Fetches stock/ETF quotes from Alpha Vantage and writes them to GlobalPriceCache.
/// Rate limits: free tier = 25 calls/day, 5 calls/minute.
/// </summary>
public class AlphaVantageService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<AlphaVantageService> _logger;

    public AlphaVantageService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<AlphaVantageService> logger)
    {
        _db     = db;
        _http   = httpClientFactory.CreateClient("AlphaVantage");
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest global quote for a symbol and upserts into GlobalPriceCache.
    /// Returns null if the API call fails or data is unavailable.
    /// </summary>
    public async Task<GlobalPriceCache?> FetchQuoteAsync(string symbol, string apiKey)
    {
        var url = $"?function=GLOBAL_QUOTE&symbol={Uri.EscapeDataString(symbol)}&apikey={apiKey}";

        AlphaVantageGlobalQuoteResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<AlphaVantageGlobalQuoteResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alpha Vantage request failed for symbol {Symbol}", symbol);
            return null;
        }

        var quote = response?.GlobalQuote;
        if (quote == null || string.IsNullOrEmpty(quote.Price) || quote.Price == "0.0000")
        {
            _logger.LogWarning("No price data from Alpha Vantage for {Symbol}", symbol);
            return null;
        }

        if (!decimal.TryParse(quote.Price, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price))
        {
            _logger.LogWarning("Could not parse price '{Price}' for {Symbol}", quote.Price, symbol);
            return null;
        }

        var asOfDate = DateTime.TryParse(quote.LatestTradingDay, out var d)
            ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        // Also store previous close so change% is available from a single fetch
        if (!string.IsNullOrEmpty(quote.PreviousClose)
            && decimal.TryParse(quote.PreviousClose, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var prevClose)
            && prevClose > 0)
        {
            var prevDate = asOfDate.AddDays(-1);
            await UpsertCacheAsync(symbol, prevClose, prevDate, InstrumentType.STOCK);
        }

        return await UpsertCacheAsync(symbol, price, asOfDate, InstrumentType.STOCK);
    }

    /// <summary>
    /// Searches Alpha Vantage SYMBOL_SEARCH for stocks/ETFs matching the query.
    /// Returns up to 10 results.
    /// </summary>
    public async Task<List<AlphaVantageSearchResult>> SearchSymbolAsync(string query, string apiKey)
    {
        var url = $"?function=SYMBOL_SEARCH&keywords={Uri.EscapeDataString(query)}&apikey={apiKey}";

        AlphaVantageSearchResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<AlphaVantageSearchResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alpha Vantage symbol search failed for query '{Query}'", query);
            return [];
        }

        return response?.BestMatches?.Take(10).ToList() ?? [];
    }

    private async Task<GlobalPriceCache> UpsertCacheAsync(
        string symbol, decimal price, DateTime asOfDate, InstrumentType instrumentType)
    {
        var existing = await _db.GlobalPriceCache
            .FirstOrDefaultAsync(g => g.Symbol == symbol && g.AsOfDate == asOfDate);

        // ASX-listed symbols end with ".AX" and are already quoted in AUD.
        // All other Alpha Vantage symbols (US exchanges) are quoted in USD.
        var currency = symbol.EndsWith(".AX", StringComparison.OrdinalIgnoreCase) ? "AUD" : "USD";

        if (existing == null)
        {
            existing = new GlobalPriceCache
            {
                Symbol         = symbol,
                InstrumentType = instrumentType,
                AsOfDate       = asOfDate,
                Price          = price,
                PriceUnit      = MetalUnit.UNIT,
                Currency       = currency,
                Source         = PriceProviderType.ALPHA_VANTAGE,
                FetchedAt      = DateTime.UtcNow
            };
            _db.GlobalPriceCache.Add(existing);
        }
        else
        {
            existing.Price     = price;
            existing.Currency  = currency;
            existing.FetchedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    // ── Response models ───────────────────────────────────────────────────────

    private record AlphaVantageGlobalQuoteResponse(
        [property: JsonPropertyName("Global Quote")] AlphaVantageQuote? GlobalQuote);

    private record AlphaVantageQuote(
        [property: JsonPropertyName("05. price")] string? Price,
        [property: JsonPropertyName("07. latest trading day")] string? LatestTradingDay,
        [property: JsonPropertyName("08. previous close")] string? PreviousClose);

    private record AlphaVantageSearchResponse(
        [property: JsonPropertyName("bestMatches")] List<AlphaVantageSearchResult>? BestMatches);
}

public record AlphaVantageSearchResult(
    [property: JsonPropertyName("1. symbol")]       string Symbol,
    [property: JsonPropertyName("2. name")]         string Name,
    [property: JsonPropertyName("3. type")]         string Type,
    [property: JsonPropertyName("4. region")]       string Region,
    [property: JsonPropertyName("8. currency")]     string Currency,
    [property: JsonPropertyName("9. matchScore")]   string MatchScore);
