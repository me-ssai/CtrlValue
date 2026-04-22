using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="InstrumentService"/> — specifically LatestPrice resolution
/// from both PriceHistory (manual) and GlobalPriceCache (API-sourced).
/// </summary>
public class InstrumentServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly InstrumentService _sut;

    public InstrumentServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new InstrumentService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetInstrumentsAsync — LatestPrice from GlobalPriceCache ──────────────

    [Fact]
    public async Task GetInstruments_WhenGlobalCacheHasAudPrice_ReturnsLatestPriceFromCache()
    {
        var instrument = SeedInstrument("GOLD.AX", externalSymbol: "GOLD.AX");
        SeedGlobalPriceCache("GOLD.AX", price: 37.50m, currency: "AUD", daysAgo: 0);

        var result = await _sut.GetInstrumentsAsync();

        result.Should().HaveCount(1);
        result[0].LatestPrice.Should().Be(37.50m);
        result[0].LatestPriceDate.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInstruments_WhenCacheHasUsdPrice_FallsBackToPriceHistory()
    {
        var instrument = SeedInstrument("AAPL", externalSymbol: "AAPL");
        SeedGlobalPriceCache("AAPL", price: 220.00m, currency: "USD", daysAgo: 0);
        SeedPriceHistory(instrument.Id, closePrice: 340.00m, daysAgo: 1);

        var result = await _sut.GetInstrumentsAsync();

        result.Should().HaveCount(1);
        result[0].LatestPrice.Should().Be(340.00m);
    }

    [Fact]
    public async Task GetInstruments_WhenBothExist_ReturnsMostRecentPrice()
    {
        var instrument = SeedInstrument("GOLD.AX", externalSymbol: "GOLD.AX");
        SeedPriceHistory(instrument.Id, closePrice: 35.00m, daysAgo: 3);
        SeedGlobalPriceCache("GOLD.AX", price: 37.50m, currency: "AUD", daysAgo: 0);

        var result = await _sut.GetInstrumentsAsync();

        result[0].LatestPrice.Should().Be(37.50m, "cache entry is newer than manual entry");
    }

    [Fact]
    public async Task GetInstruments_WhenManualEntryIsNewer_ReturnsPriceHistoryPrice()
    {
        var instrument = SeedInstrument("GOLD.AX", externalSymbol: "GOLD.AX");
        SeedGlobalPriceCache("GOLD.AX", price: 35.00m, currency: "AUD", daysAgo: 2);
        SeedPriceHistory(instrument.Id, closePrice: 37.50m, daysAgo: 0);

        var result = await _sut.GetInstrumentsAsync();

        result[0].LatestPrice.Should().Be(37.50m, "manual entry is newer than cache entry");
    }

    [Fact]
    public async Task GetInstruments_WhenCacheSymbolMatchesExternalSymbol_ReturnsPrice()
    {
        // Instrument has Symbol="GOLD", ExternalSymbol="GOLD.AX"
        // GlobalPriceCache stores under "GOLD.AX"
        var instrument = SeedInstrument("GOLD", externalSymbol: "GOLD.AX");
        SeedGlobalPriceCache("GOLD.AX", price: 37.50m, currency: "AUD", daysAgo: 0);

        var result = await _sut.GetInstrumentsAsync();

        result[0].LatestPrice.Should().Be(37.50m);
    }

    [Fact]
    public async Task GetInstruments_WhenNoPriceData_ReturnsNullLatestPrice()
    {
        SeedInstrument("NEWSTOCK");

        var result = await _sut.GetInstrumentsAsync();

        result[0].LatestPrice.Should().BeNull();
        result[0].LatestPriceDate.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Instrument SeedInstrument(string symbol, string? externalSymbol = null)
    {
        var instrument = new Instrument
        {
            Symbol         = symbol,
            Name           = $"{symbol} Instrument",
            InstrumentType = InstrumentType.STOCK,
            Currency       = "AUD",
            ExternalSymbol = externalSymbol,
            PriceProvider  = PriceProviderType.YAHOO_FINANCE,
            TenantId       = "default"
        };
        _db.Instruments.Add(instrument);
        _db.SaveChanges();
        return instrument;
    }

    private void SeedGlobalPriceCache(string symbol, decimal price, string currency, int daysAgo)
    {
        _db.GlobalPriceCache.Add(new GlobalPriceCache
        {
            Symbol         = symbol,
            InstrumentType = InstrumentType.STOCK,
            Price          = price,
            Currency       = currency,
            PriceUnit      = MetalUnit.UNIT,
            Source         = PriceProviderType.YAHOO_FINANCE,
            AsOfDate       = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-daysAgo), DateTimeKind.Utc),
            FetchedAt      = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private void SeedPriceHistory(Guid instrumentId, decimal closePrice, int daysAgo)
    {
        _db.PriceHistory.Add(new PriceHistory
        {
            InstrumentId = instrumentId,
            ClosePrice   = closePrice,
            Currency     = "AUD",
            AsOfDate     = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-daysAgo), DateTimeKind.Utc),
            TenantId     = "default"
        });
        _db.SaveChanges();
    }
}
