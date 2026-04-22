using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Jobs;

/// <summary>
/// Background job that runs every 24 hours and fetches live prices for:
///   1. All instruments with at least one active position
///   2. All instruments marked IsDefault = true (shown on the ticker strip)
///
/// Provider priority per entity:
///   1. Entity's own API key (stored encrypted in EntityIntegration)
///   2. Platform-level admin key (PlatformIntegration table — super admin managed)
///   3. Platform-level fallback key from configuration (appsettings)
///
/// Alpha Vantage free tier: 25 calls/day, 5 calls/min — job throttles accordingly.
/// Metals API free tier: 50 calls/month — one call fetches all metals.
/// CoinGecko public API: no key required for basic price endpoints.
/// </summary>
public class PriceFetchJob : BackgroundService
{
    private static readonly TimeSpan RunInterval       = TimeSpan.FromHours(24);
    private static readonly TimeSpan AlphaVantageDelay = TimeSpan.FromSeconds(13); // ~4.6/min, safely under 5/min

    private readonly IServiceProvider _services;
    private readonly ILogger<PriceFetchJob> _logger;

    public PriceFetchJob(IServiceProvider services, ILogger<PriceFetchJob> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceFetchJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PriceFetchJob encountered an unexpected error.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope          = _services.CreateScope();
        var db                   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var integrationService   = scope.ServiceProvider.GetRequiredService<IEntityIntegrationService>();
        var alphaVantageService  = scope.ServiceProvider.GetRequiredService<AlphaVantageService>();
        var metalsPriceService   = scope.ServiceProvider.GetRequiredService<MetalsPriceService>();
        var coinGeckoService     = scope.ServiceProvider.GetRequiredService<CoinGeckoService>();
        var yahooFinanceService  = scope.ServiceProvider.GetRequiredService<YahooFinanceService>();

        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        // ── Anonymous helper type shared by both queries ──────────────────────
        // (C# anonymous types can't be declared once and reused across queries,
        //  so we use a named tuple-like approach with a local record.)

        // ── Instruments with active positions ────────────────────────────────
        var withPositions = await db.Instruments
            .Where(i => !i.IsDeleted
                     && i.PriceProvider != null
                     && i.PriceProvider != PriceProviderType.MANUAL
                     && i.Positions.Any(p => !p.IsDeleted))
            .Select(i => new InstrumentFetchTarget(
                i.Id,
                i.ExternalSymbol ?? i.Symbol,
                i.InstrumentType,
                i.PriceProvider!.Value,
                i.Positions
                    .Where(p => !p.IsDeleted)
                    .Select(p => p.Account.EntityId)
                    .Distinct()
                    .ToList()))
            .ToListAsync(ct);

        // ── Default instruments (ticker strip) ───────────────────────────────
        var defaults = await db.Instruments
            .Where(i => i.IsDefault && !i.IsDeleted
                     && i.PriceProvider != null
                     && i.PriceProvider != PriceProviderType.MANUAL)
            .Select(i => new InstrumentFetchTarget(
                i.Id,
                i.ExternalSymbol ?? i.Symbol,
                i.InstrumentType,
                i.PriceProvider!.Value,
                new List<Guid>())) // no entity context — will use platform key
            .ToListAsync(ct);

        // Merge: position instruments take precedence (they carry entity IDs for key lookup)
        var positionSymbols = withPositions.Select(x => x.Symbol).ToHashSet();
        var allInstruments = withPositions
            .Concat(defaults.Where(d => !positionSymbols.Contains(d.Symbol)))
            .ToList();

        _logger.LogInformation("PriceFetchJob: {WithPos} instruments with positions + {Defaults} default-only = {Total} total.",
            withPositions.Count, defaults.Count(d => !positionSymbols.Contains(d.Symbol)), allInstruments.Count);

        int fetched = 0, skipped = 0, errors = 0;

        // ── Metals API ────────────────────────────────────────────────────────
        var metalInstruments = allInstruments
            .Where(i => i.PriceProvider == PriceProviderType.METALS_API)
            .ToList();

        if (metalInstruments.Any())
        {
            var metalSymbols  = metalInstruments.Select(i => i.Symbol).ToHashSet();
            var alreadyCached = await db.GlobalPriceCache
                .Where(g => metalSymbols.Contains(g.Symbol) && g.AsOfDate == today)
                .Select(g => g.Symbol)
                .ToListAsync(ct);

            var symbolsNeedingFetch = metalSymbols.Except(alreadyCached).ToList();

            if (symbolsNeedingFetch.Any())
            {
                // Try entity keys first, then fall back to platform key (empty entityIds → null from entity query → uses platform key)
                var entityIds = metalInstruments.SelectMany(i => i.EntityIds).Distinct();
                string? apiKey = null;
                foreach (var entityId in entityIds)
                {
                    apiKey = await integrationService.GetEffectiveApiKeyAsync(entityId, "METALS_API");
                    if (apiKey != null) break;
                }

                // If no entity had a key, try platform key directly (for default-only instruments)
                apiKey ??= await integrationService.GetEffectiveApiKeyAsync(Guid.Empty, "METALS_API");

                if (apiKey != null)
                {
                    try
                    {
                        var results = await metalsPriceService.FetchSpotPricesAsync(apiKey, "AUD");
                        fetched += results.Count;
                        _logger.LogInformation("Metals: fetched {Count} spot prices.", results.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Metals API fetch failed.");
                        errors++;
                    }
                }
                else
                {
                    _logger.LogWarning("No Metals API key configured — skipping metal price fetch.");
                    skipped += symbolsNeedingFetch.Count;
                }
            }
            else
            {
                skipped += metalSymbols.Count;
                _logger.LogDebug("Metals: all prices already cached for today.");
            }
        }

        // ── Alpha Vantage (Stocks / ETFs) ─────────────────────────────────────
        var stockInstruments = allInstruments
            .Where(i => i.PriceProvider == PriceProviderType.ALPHA_VANTAGE)
            .ToList();

        if (stockInstruments.Any())
        {
            var stockSymbols  = stockInstruments.Select(i => i.Symbol).ToHashSet();
            var alreadyCached = await db.GlobalPriceCache
                .Where(g => stockSymbols.Contains(g.Symbol) && g.AsOfDate == today)
                .Select(g => g.Symbol)
                .ToListAsync(ct);

            var symbolsNeedingFetch = stockSymbols.Except(alreadyCached).ToList();

            const int DailyLimit = 25;
            var toFetch = symbolsNeedingFetch.Take(DailyLimit).ToList();

            if (symbolsNeedingFetch.Count > DailyLimit)
                _logger.LogWarning("Alpha Vantage: {Total} symbols need prices but free tier caps at {Limit}/day.",
                    symbolsNeedingFetch.Count, DailyLimit);

            foreach (var symbol in toFetch)
            {
                if (ct.IsCancellationRequested) break;

                var instrument = stockInstruments.First(i => i.Symbol == symbol);
                string? apiKey = null;
                foreach (var entityId in instrument.EntityIds)
                {
                    apiKey = await integrationService.GetEffectiveApiKeyAsync(entityId, "ALPHA_VANTAGE");
                    if (apiKey != null) break;
                }

                // Fall back to platform key for default instruments with no entity holders
                apiKey ??= await integrationService.GetEffectiveApiKeyAsync(Guid.Empty, "ALPHA_VANTAGE");

                if (apiKey == null)
                {
                    _logger.LogWarning("No Alpha Vantage key for {Symbol} — skipping.", symbol);
                    skipped++;
                    continue;
                }

                try
                {
                    var result = await alphaVantageService.FetchQuoteAsync(symbol, apiKey);
                    if (result != null)
                    {
                        fetched++;
                        _logger.LogDebug("AV: fetched {Symbol} = {Price}", symbol, result.Price);
                    }
                    else
                    {
                        errors++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Alpha Vantage fetch failed for {Symbol}.", symbol);
                    errors++;
                }

                await Task.Delay(AlphaVantageDelay, ct);
            }

            skipped += symbolsNeedingFetch.Count - toFetch.Count + alreadyCached.Count;
        }

        // ── Yahoo Finance (ASX and other markets, no API key required) ────────
        var yahooInstruments = allInstruments
            .Where(i => i.PriceProvider == PriceProviderType.YAHOO_FINANCE)
            .ToList();

        if (yahooInstruments.Any())
        {
            var yahooSymbols  = yahooInstruments.Select(i => i.Symbol).ToHashSet();
            var alreadyCachedYahoo = await db.GlobalPriceCache
                .Where(g => yahooSymbols.Contains(g.Symbol) && g.AsOfDate == today)
                .Select(g => g.Symbol)
                .ToListAsync(ct);

            foreach (var instrument in yahooInstruments)
            {
                if (ct.IsCancellationRequested) break;
                if (alreadyCachedYahoo.Contains(instrument.Symbol)) { skipped++; continue; }

                try
                {
                    var result = await yahooFinanceService.FetchQuoteAsync(instrument.Symbol);
                    if (result != null)
                    {
                        fetched++;
                        _logger.LogDebug("Yahoo: fetched {Symbol} = {Price}", instrument.Symbol, result.Price);
                    }
                    else
                    {
                        errors++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Yahoo Finance fetch failed for {Symbol}.", instrument.Symbol);
                    errors++;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            skipped += alreadyCachedYahoo.Count;
        }

        // ── CoinGecko (Crypto) ────────────────────────────────────────────────
        var cryptoInstruments = allInstruments
            .Where(i => i.PriceProvider == PriceProviderType.COINGECKO)
            .ToList();

        if (cryptoInstruments.Any())
        {
            var cryptoSymbols = cryptoInstruments.Select(i => i.Symbol).ToHashSet();
            var alreadyCached = await db.GlobalPriceCache
                .Where(g => cryptoSymbols.Contains(g.Symbol) && g.AsOfDate == today)
                .Select(g => g.Symbol)
                .ToListAsync(ct);

            var toFetch = cryptoInstruments
                .Where(i => !alreadyCached.Contains(i.Symbol))
                .ToList();

            foreach (var instrument in toFetch)
            {
                if (ct.IsCancellationRequested) break;

                // ExternalSymbol holds the CoinGecko coin ID (e.g. "bitcoin")
                // Symbol holds the ticker (e.g. "BTC")
                var coinId = instrument.Symbol; // fallback if ExternalSymbol not set

                // Look up the instrument's ExternalSymbol from DB
                var dbInstrument = await db.Instruments
                    .FirstOrDefaultAsync(i => i.Symbol == instrument.Symbol && !i.IsDeleted, ct);
                if (dbInstrument?.ExternalSymbol != null)
                    coinId = dbInstrument.ExternalSymbol;

                try
                {
                    var result = await coinGeckoService.FetchPriceAsync(coinId, instrument.Symbol);
                    if (result != null)
                    {
                        fetched++;
                        _logger.LogDebug("CoinGecko: fetched {Symbol} = {Price}", instrument.Symbol, result.Price);
                    }
                    else
                    {
                        errors++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CoinGecko fetch failed for {Symbol}.", instrument.Symbol);
                    errors++;
                }
            }

            skipped += alreadyCached.Count;
        }

        _logger.LogInformation(
            "PriceFetchJob complete: fetched={Fetched}, skipped={Skipped}, errors={Errors}",
            fetched, skipped, errors);
    }

    // ── Local record for cross-query instrument projection ────────────────────

    private record InstrumentFetchTarget(
        Guid Id,
        string Symbol,
        InstrumentType InstrumentType,
        PriceProviderType PriceProvider,
        List<Guid> EntityIds);
}
