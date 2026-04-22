using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace CtrlValue.Application.Services;

/// <summary>
/// Singleton HTTP client for Yahoo Finance's unofficial API.
/// No API key or crumb required — the search and chart endpoints work with
/// browser-like headers alone (confirmed via direct API testing).
/// Singleton so the underlying socket pool is reused across all requests.
/// </summary>
public sealed class YahooHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<YahooHttpClient> _logger;

    public YahooHttpClient(ILogger<YahooHttpClient> logger)
    {
        _logger = logger;
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
            Timeout     = TimeSpan.FromSeconds(10)
        };
        _client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _client.DefaultRequestHeaders.Add("Accept", "application/json,*/*");
        _client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _client.DefaultRequestHeaders.Add("Referer", "https://finance.yahoo.com/");
    }

    /// <summary>
    /// Makes a GET request to Yahoo Finance.
    /// Returns default(T) on any failure — callers handle null gracefully.
    /// </summary>
    public async Task<T?> GetJsonAsync<T>(string relativeUrl)
    {
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(relativeUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo Finance request failed: {Url}", relativeUrl);
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Yahoo Finance returned {Status} for {Url}", response.StatusCode, relativeUrl);
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo Finance JSON parse failed for {Url}", relativeUrl);
            return default;
        }
    }
}
