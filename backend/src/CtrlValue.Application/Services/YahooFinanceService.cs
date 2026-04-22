using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Fetches stock/ETF quotes and performs symbol searches via Yahoo Finance's
/// unofficial JSON API. Requires a crumb + session cookie (see YahooHttpClient).
///
/// Supports ASX symbols with the .AX suffix (e.g. GOLD.AX, CBA.AX).
/// Currency is taken directly from Yahoo's response so AUD is returned
/// automatically for .AX instruments.
/// </summary>
public class YahooFinanceService
{
    private readonly AppDbContext _db;
    private readonly YahooHttpClient _yahoo;
    private readonly ILogger<YahooFinanceService> _logger;

    public YahooFinanceService(
        AppDbContext db,
        YahooHttpClient yahoo,
        ILogger<YahooFinanceService> logger)
    {
        _db     = db;
        _yahoo  = yahoo;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest quote for a symbol and upserts into GlobalPriceCache.
    /// Symbol should include the exchange suffix where applicable (e.g. GOLD.AX).
    /// Returns null if the request fails or no price data is available.
    /// </summary>
    public async Task<GlobalPriceCache?> FetchQuoteAsync(string symbol)
    {
        var url = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=2d";

        var response = await _yahoo.GetJsonAsync<YahooChartResponse>(url);
        if (response == null)
        {
            _logger.LogWarning("Yahoo Finance request failed or returned null for {Symbol}", symbol);
            return null;
        }

        var meta = response.Chart?.Result?.FirstOrDefault()?.Meta;
        if (meta == null)
        {
            _logger.LogWarning("No chart data from Yahoo Finance for {Symbol}", symbol);
            return null;
        }

        var price = meta.RegularMarketPrice;
        if (price <= 0)
        {
            _logger.LogWarning("Zero or negative price from Yahoo Finance for {Symbol}", symbol);
            return null;
        }

        var currency = meta.Currency ?? (symbol.EndsWith(".AX", StringComparison.OrdinalIgnoreCase) ? "AUD" : "USD");
        var asOfDate = meta.RegularMarketTime > 0
            ? DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(meta.RegularMarketTime).UtcDateTime.Date, DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        return await UpsertCacheAsync(symbol, price, asOfDate, currency);
    }

    /// <summary>
    /// Searches Yahoo Finance for matching symbols. Optionally scoped to a region
    /// (e.g. "AU" for ASX results). Returns up to 15 results.
    /// </summary>
    public async Task<List<YahooSearchResult>> SearchAsync(string query, string? region = null)
    {
        var regionParam = string.IsNullOrEmpty(region) ? "" : $"&region={Uri.EscapeDataString(region)}&lang=en-{region}";
        var url = $"v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=15&newsCount=0&enableFuzzyQuery=false{regionParam}";

        var response = await _yahoo.GetJsonAsync<YahooSearchResponse>(url);
        if (response == null) return [];

        return response.Quotes?
            .Where(q => !string.IsNullOrEmpty(q.Symbol))
            .Select(q => new YahooSearchResult(
                q.Symbol!,
                q.Shortname ?? q.Longname ?? q.Symbol!,
                MapQuoteType(q.QuoteType),
                q.Exchange,
                null)) // currency not returned in search; resolved on price fetch
            .Take(15)
            .ToList() ?? [];
    }

    private static string MapQuoteType(string? quoteType) =>
        quoteType?.ToUpperInvariant() switch
        {
            "ETF"    => "ETF",
            "EQUITY" => "STOCK",
            _        => "STOCK"
        };

    private async Task<GlobalPriceCache> UpsertCacheAsync(
        string symbol, decimal price, DateTime asOfDate, string currency)
    {
        var existing = await _db.GlobalPriceCache
            .FirstOrDefaultAsync(g => g.Symbol == symbol && g.AsOfDate == asOfDate);

        if (existing == null)
        {
            existing = new GlobalPriceCache
            {
                Symbol         = symbol,
                InstrumentType = InstrumentType.STOCK,
                AsOfDate       = asOfDate,
                Price          = price,
                PriceUnit      = MetalUnit.UNIT,
                Currency       = currency,
                Source         = PriceProviderType.YAHOO_FINANCE,
                FetchedAt      = DateTime.UtcNow
            };
            _db.GlobalPriceCache.Add(existing);
        }
        else
        {
            existing.Price     = price;
            existing.Currency  = currency;
            existing.Source    = PriceProviderType.YAHOO_FINANCE;
            existing.FetchedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    // ── Response models ───────────────────────────────────────────────────────

    private record YahooChartResponse(
        [property: JsonPropertyName("chart")] YahooChart? Chart);

    private record YahooChart(
        [property: JsonPropertyName("result")] YahooChartResult[]? Result);

    private record YahooChartResult(
        [property: JsonPropertyName("meta")] YahooChartMeta Meta);

    private record YahooChartMeta(
        [property: JsonPropertyName("regularMarketPrice")] decimal RegularMarketPrice,
        [property: JsonPropertyName("currency")]           string? Currency,
        [property: JsonPropertyName("regularMarketTime")] long    RegularMarketTime);

    private record YahooSearchResponse(
        [property: JsonPropertyName("quotes")] YahooSearchQuote[]? Quotes);

    private record YahooSearchQuote(
        [property: JsonPropertyName("symbol")]    string?  Symbol,
        [property: JsonPropertyName("shortname")] string?  Shortname,
        [property: JsonPropertyName("longname")]  string?  Longname,
        [property: JsonPropertyName("exchange")]  string?  Exchange,
        [property: JsonPropertyName("quoteType")] string?  QuoteType);
}

public record YahooSearchResult(
    string  Symbol,
    string  Name,
    string  Type,
    string? Exchange,
    string? Currency);
