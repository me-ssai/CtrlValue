using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Infrastructure.Data.Seeders;

/// <summary>
/// Runs once on startup and ensures the curated set of default instruments
/// (IsDefault = true) exists in the instrument table.
/// These are the fallback tickers shown on the ticker strip to users who have
/// no personal holdings, and they are also fetched by the nightly PriceFetchJob.
/// </summary>
public class DefaultInstrumentSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DefaultInstrumentSeeder> _logger;

    // ── Curated default instruments ──────────────────────────────────────────

    private static readonly Instrument[] Defaults =
    [
        // Stocks (Alpha Vantage)
        new() { Symbol = "AAPL",  Name = "Apple Inc.",         InstrumentType = InstrumentType.STOCK,  PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true },
        new() { Symbol = "MSFT",  Name = "Microsoft Corp.",    InstrumentType = InstrumentType.STOCK,  PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true },
        new() { Symbol = "GOOGL", Name = "Alphabet Inc.",      InstrumentType = InstrumentType.STOCK,  PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true },
        new() { Symbol = "AMZN",  Name = "Amazon.com Inc.",    InstrumentType = InstrumentType.STOCK,  PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true },
        new() { Symbol = "NVDA",  Name = "NVIDIA Corp.",       InstrumentType = InstrumentType.STOCK,  PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true },
        // ETFs (Alpha Vantage)
        new() { Symbol = "SPY",   Name = "S&P 500 ETF",        InstrumentType = InstrumentType.ETF,    PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, Exchange = "NYSE Arca" },
        new() { Symbol = "QQQ",   Name = "Nasdaq-100 ETF",     InstrumentType = InstrumentType.ETF,    PriceProvider = PriceProviderType.ALPHA_VANTAGE, Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, Exchange = "Nasdaq" },
        // Metals (Metals API)
        new() { Symbol = "XAU",   Name = "Gold",               InstrumentType = InstrumentType.METAL,  PriceProvider = PriceProviderType.METALS_API,    Currency = "USD", PriceUnit = MetalUnit.TROY_OZ, IsDefault = true },
        new() { Symbol = "XAG",   Name = "Silver",             InstrumentType = InstrumentType.METAL,  PriceProvider = PriceProviderType.METALS_API,    Currency = "USD", PriceUnit = MetalUnit.TROY_OZ, IsDefault = true },
        new() { Symbol = "XPT",   Name = "Platinum",           InstrumentType = InstrumentType.METAL,  PriceProvider = PriceProviderType.METALS_API,    Currency = "USD", PriceUnit = MetalUnit.TROY_OZ, IsDefault = true },
        new() { Symbol = "XPD",   Name = "Palladium",          InstrumentType = InstrumentType.METAL,  PriceProvider = PriceProviderType.METALS_API,    Currency = "USD", PriceUnit = MetalUnit.TROY_OZ, IsDefault = true },
        // Crypto (CoinGecko)
        new() { Symbol = "BTC",   Name = "Bitcoin",            InstrumentType = InstrumentType.CRYPTO, PriceProvider = PriceProviderType.COINGECKO,     Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, ExternalSymbol = "bitcoin" },
        new() { Symbol = "ETH",   Name = "Ethereum",           InstrumentType = InstrumentType.CRYPTO, PriceProvider = PriceProviderType.COINGECKO,     Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, ExternalSymbol = "ethereum" },
        new() { Symbol = "SOL",   Name = "Solana",             InstrumentType = InstrumentType.CRYPTO, PriceProvider = PriceProviderType.COINGECKO,     Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, ExternalSymbol = "solana" },
        new() { Symbol = "BNB",   Name = "BNB",                InstrumentType = InstrumentType.CRYPTO, PriceProvider = PriceProviderType.COINGECKO,     Currency = "USD", PriceUnit = MetalUnit.UNIT, IsDefault = true, ExternalSymbol = "binancecoin" },
    ];

    public DefaultInstrumentSeeder(IServiceProvider services, ILogger<DefaultInstrumentSeeder> logger)
    {
        _services = services;
        _logger   = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingSymbolsList = await db.Instruments
            .Where(i => !i.IsDeleted)
            .Select(i => i.Symbol)
            .ToListAsync(cancellationToken);
        var existingSymbols = existingSymbolsList.ToHashSet();

        var toInsert = Defaults
            .Where(d => !existingSymbols.Contains(d.Symbol))
            .ToList();

        if (toInsert.Count == 0)
        {
            _logger.LogInformation("DefaultInstrumentSeeder: all {Count} default instruments already present.", Defaults.Length);
            return;
        }

        // Assign new IDs (entity initializers above don't set Id)
        foreach (var instrument in toInsert)
            instrument.Id = Guid.NewGuid();

        db.Instruments.AddRange(toInsert);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("DefaultInstrumentSeeder: seeded {Count} new default instruments.", toInsert.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
