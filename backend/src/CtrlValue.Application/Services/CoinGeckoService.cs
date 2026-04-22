using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Fetches cryptocurrency prices from CoinGecko and writes them to GlobalPriceCache.
/// Also supports symbol search for the instrument autocomplete feature.
/// CoinGecko public API: no API key required for basic price/search endpoints.
/// </summary>
public class CoinGeckoService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<CoinGeckoService> _logger;

    public CoinGeckoService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<CoinGeckoService> logger)
    {
        _db     = db;
        _http   = httpClientFactory.CreateClient("CoinGecko");
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest price for a coin by its CoinGecko ID (e.g. "bitcoin", "ethereum").
    /// Returns null if the fetch fails.
    /// </summary>
    public async Task<GlobalPriceCache?> FetchPriceAsync(string coinId, string symbol)
    {
        var url = $"simple/price?ids={Uri.EscapeDataString(coinId)}&vs_currencies=aud&include_24hr_change=true";

        CoinGeckoPriceResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<CoinGeckoPriceResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinGecko request failed for coin {CoinId}", coinId);
            return null;
        }

        if (response == null || !response.TryGetValue(coinId, out var priceData)
            || !priceData.TryGetValue("aud", out var price) || price <= 0)
        {
            _logger.LogWarning("No price data from CoinGecko for {CoinId}", coinId);
            return null;
        }

        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        // Derive approximate previous price from 24h change so change% shows immediately
        if (priceData.TryGetValue("aud_24h_change", out var change24h) && change24h != 0)
        {
            var prevPrice = price / (1m + change24h / 100m);
            if (prevPrice > 0)
                await UpsertCacheAsync(symbol, coinId, Math.Round(prevPrice, 8), today.AddDays(-1));
        }

        return await UpsertCacheAsync(symbol, coinId, price, today);
    }

    /// <summary>
    /// Searches CoinGecko for coins matching a query string.
    /// Returns up to 10 results.
    /// </summary>
    public async Task<List<CoinGeckoSearchResult>> SearchAsync(string query)
    {
        var url = $"search?query={Uri.EscapeDataString(query)}";

        CoinGeckoSearchResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<CoinGeckoSearchResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinGecko search failed for query '{Query}'", query);
            return [];
        }

        return response?.Coins?.Take(10).ToList() ?? [];
    }

    private async Task<GlobalPriceCache> UpsertCacheAsync(
        string symbol, string coinId, decimal price, DateTime asOfDate)
    {
        var existing = await _db.GlobalPriceCache
            .FirstOrDefaultAsync(g => g.Symbol == symbol && g.AsOfDate == asOfDate);

        if (existing == null)
        {
            existing = new GlobalPriceCache
            {
                Symbol         = symbol,
                InstrumentType = InstrumentType.CRYPTO,
                AsOfDate       = asOfDate,
                Price          = price,
                PriceUnit      = MetalUnit.UNIT,
                Currency       = "AUD",
                Source         = PriceProviderType.COINGECKO,
                FetchedAt      = DateTime.UtcNow
            };
            _db.GlobalPriceCache.Add(existing);
        }
        else
        {
            existing.Price     = price;
            existing.FetchedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    // ── Response models ────────────────────────────────────────────────────────

    // Simple price: { "bitcoin": { "usd": 82000.00 } }
    private class CoinGeckoPriceResponse : Dictionary<string, Dictionary<string, decimal>> { }

    private record CoinGeckoSearchResponse(
        [property: JsonPropertyName("coins")] List<CoinGeckoSearchResult>? Coins);

    public record CoinGeckoSearchResult(
        [property: JsonPropertyName("id")]     string Id,
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("name")]   string Name,
        [property: JsonPropertyName("market_cap_rank")] int? MarketCapRank);
}
