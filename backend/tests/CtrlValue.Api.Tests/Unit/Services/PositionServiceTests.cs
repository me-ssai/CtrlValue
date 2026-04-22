using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="PositionService"/> — focusing on gain/loss calculation
/// when cost basis is zero or null.
/// </summary>
public class PositionServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly PositionService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;

    public PositionServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new PositionService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetPositions — zero / null cost basis shows 0 gain/loss ─────────────

    [Fact]
    public async Task GetPositions_WhenCostBasisIsZero_ReturnsZeroGainLoss()
    {
        var (_, instrument) = SeedAccountAndInstrument();
        SeedGlobalPriceCache(instrument.ExternalSymbol!, price: 50.00m, currency: "AUD");
        SeedPosition(instrument, quantity: 10, costBasis: 0m);

        var result = await _sut.GetPositionsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].UnrealizedGainLoss.Should().Be(0m);
        result[0].UnrealizedGainLossPercent.Should().Be(0m);
    }

    [Fact]
    public async Task GetPositions_WhenCostBasisIsNull_ReturnsZeroGainLoss()
    {
        var (_, instrument) = SeedAccountAndInstrument();
        SeedGlobalPriceCache(instrument.ExternalSymbol!, price: 50.00m, currency: "AUD");
        SeedPosition(instrument, quantity: 10, costBasis: null);

        var result = await _sut.GetPositionsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].UnrealizedGainLoss.Should().Be(0m);
        result[0].UnrealizedGainLossPercent.Should().Be(0m);
    }

    [Fact]
    public async Task GetPositions_WhenCostBasisIsNonZero_ReturnsCorrectGainLoss()
    {
        var (_, instrument) = SeedAccountAndInstrument();
        SeedGlobalPriceCache(instrument.ExternalSymbol!, price: 60.00m, currency: "AUD");
        SeedPosition(instrument, quantity: 10, costBasis: 500.00m);

        var result = await _sut.GetPositionsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].UnrealizedGainLoss.Should().Be(100.00m, "60*10=600 current - 500 cost = 100");
        result[0].UnrealizedGainLossPercent.Should().BeApproximately(20m, 0.01m);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (Account account, Instrument instrument) SeedAccountAndInstrument()
    {
        var account = new Account
        {
            EntityId   = _entityId,
            Name       = "Test Account",
            AccountType = AccountType.ASSET,
            Currency   = "AUD",
            TenantId   = "default"
        };
        _db.Accounts.Add(account);

        var instrument = new Instrument
        {
            Symbol         = "GOLD.AX",
            Name           = "ETFS Physical Gold",
            InstrumentType = InstrumentType.STOCK,
            Currency       = "AUD",
            ExternalSymbol = "GOLD.AX",
            PriceProvider  = PriceProviderType.YAHOO_FINANCE,
            TenantId       = "default"
        };
        _db.Instruments.Add(instrument);
        _db.SaveChanges();

        return (account, instrument);
    }

    private void SeedGlobalPriceCache(string symbol, decimal price, string currency)
    {
        _db.GlobalPriceCache.Add(new GlobalPriceCache
        {
            Symbol         = symbol,
            InstrumentType = InstrumentType.STOCK,
            Price          = price,
            Currency       = currency,
            PriceUnit      = MetalUnit.UNIT,
            Source         = PriceProviderType.YAHOO_FINANCE,
            AsOfDate       = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc),
            FetchedAt      = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private void SeedPosition(Instrument instrument, decimal quantity, decimal? costBasis)
    {
        var account = _db.Accounts.First(a => a.EntityId == _entityId);
        _db.Positions.Add(new Position
        {
            AccountId      = account.Id,
            InstrumentId   = instrument.Id,
            Quantity       = quantity,
            Unit           = MetalUnit.UNIT,
            CostBasisTotal = costBasis,
            OpenedAt       = DateTime.UtcNow,
            TenantId       = "default"
        });
        _db.SaveChanges();
    }
}
