using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Fetches precious metal spot prices from metalpriceapi.com and writes them to GlobalPriceCache.
/// One API call returns all metals (XAU, XAG, XPT, XPD) — minimises monthly quota usage.
/// Free tier: 100 requests/month (no credit card required — sign up at metalpriceapi.com).
/// </summary>
public class MetalsPriceService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<MetalsPriceService> _logger;

    // Metals to fetch — metalpriceapi.com symbol codes (same XAU/XAG standard)
    private static readonly string[] MetalSymbols = { "XAU", "XAG", "XPT", "XPD" };

    public MetalsPriceService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<MetalsPriceService> logger)
    {
        _db     = db;
        _http   = httpClientFactory.CreateClient("MetalsApi");
        _logger = logger;
    }

    /// <summary>
    /// Fetches latest spot prices for all tracked metals.
    /// Returns the list of cache rows that were written.
    /// </summary>
    public async Task<List<GlobalPriceCache>> FetchSpotPricesAsync(string apiKey, string baseCurrency = "USD")
    {
        var symbols = string.Join(",", MetalSymbols);
        var url     = $"v1/latest?api_key={apiKey}&base={baseCurrency}&symbols={symbols}";

        MetalsApiResponse? response;
        try
        {
            response = await _http.GetFromJsonAsync<MetalsApiResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metals API request failed");
            return [];
        }

        if (response?.Success != true || response.Rates == null)
        {
            _logger.LogWarning("Metals API returned unsuccessful response or no rates");
            return [];
        }

        var results = new List<GlobalPriceCache>();
        var today   = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        foreach (var symbol in MetalSymbols)
        {
            if (!response.Rates.TryGetValue(symbol, out var rate) || rate <= 0)
                continue;

            // metalpriceapi with base=USD returns rate as oz-per-dollar (inverse of price).
            // e.g. XAU rate ≈ 0.000345 means 1 USD buys 0.000345 oz gold.
            // Invert to get USD-per-troy-oz: 1 / 0.000345 ≈ $2898
            var price = Math.Round(1m / rate, 2);

            var cached = await UpsertCacheAsync(symbol, price, today, baseCurrency);
            results.Add(cached);
            _logger.LogInformation("Metal {Symbol}: {Price} {Currency}/troy oz", symbol, price, baseCurrency);
        }

        return results;
    }

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
                InstrumentType = InstrumentType.METAL,
                AsOfDate       = asOfDate,
                Price          = price,
                PriceUnit      = MetalUnit.TROY_OZ,
                Currency       = currency,
                Source         = PriceProviderType.METALS_API,
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

    private record MetalsApiResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
}
