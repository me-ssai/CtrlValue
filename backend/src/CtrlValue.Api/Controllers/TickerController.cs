using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TickerController : ControllerBase
{
    private readonly AppDbContext _db;

    public TickerController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns ticker strip items for instruments held by the current entity.
    /// Only instruments with active positions are shown — no platform-wide defaults.
    /// For each item: today's price, day-over-day change, and position summary.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TickerItemDto>>> GetTicker()
    {
        var entityId = GetEntityId();
        // Look back up to 7 days — stock markets are closed on weekends/holidays,
        // so "latest trading day" may be several days old.
        var cutoff = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-7), DateTimeKind.Utc);

        // ── User's held instruments (entity-scoped) ───────────────────────────
        List<UserHolding> userHoldings = [];

        if (entityId.HasValue)
        {
            userHoldings = await _db.Positions
                .Where(p => !p.IsDeleted
                         && p.Account.EntityId == entityId.Value
                         && p.InstrumentId != null)
                .Select(p => new UserHolding(
                    p.Instrument!.Symbol,
                    p.Instrument.ExternalSymbol,
                    p.Instrument.Name,
                    p.Instrument.InstrumentType,
                    p.Instrument.PriceUnit,
                    p.Instrument.Currency,
                    p.Quantity,
                    p.CostBasisTotal))
                .ToListAsync();
        }

        if (userHoldings.Count == 0)
            return Ok(new List<TickerItemDto>());

        // ── Symbols to look up — include ExternalSymbol so .AX prices are found ──
        // GlobalPriceCache stores prices under ExternalSymbol (e.g. "GOLD.AX")
        // but the instrument's Symbol may be different (e.g. "GOLD").
        var allSymbols = userHoldings
            .SelectMany(h => new[] { h.Symbol, h.ExternalSymbol }.Where(s => s != null)!)
            .ToHashSet();

        // ── Fetch the two most recent price rows per symbol (for change %) ─────
        var allPrices = await _db.GlobalPriceCache
            .Where(g => allSymbols.Contains(g.Symbol) && g.AsOfDate >= cutoff)
            .OrderByDescending(g => g.AsOfDate)
            .ToListAsync();

        var pricesBySymbol = allPrices
            .GroupBy(p => p.Symbol)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var ordered = g.OrderByDescending(p => p.AsOfDate).ToList();
                    return (current: ordered[0], previous: ordered.Count > 1 ? ordered[1] : null);
                });

        GlobalPriceCache? TodayPrice(string symbol) =>
            pricesBySymbol.TryGetValue(symbol, out var pair) ? pair.current : null;
        GlobalPriceCache? PrevPrice(string symbol) =>
            pricesBySymbol.TryGetValue(symbol, out var pair) ? pair.previous : null;

        // Prefer ExternalSymbol for cache lookup (where price is actually stored)
        string PriceSymbol(UserHolding h) =>
            (h.ExternalSymbol != null && pricesBySymbol.ContainsKey(h.ExternalSymbol))
                ? h.ExternalSymbol
                : h.Symbol;

        var results = new List<TickerItemDto>();

        // De-duplicate: if the same instrument appears in multiple positions, show it once
        foreach (var holding in userHoldings.DistinctBy(h => h.Symbol))
        {
            var priceSymbol    = PriceSymbol(holding);
            var todayPrice     = TodayPrice(priceSymbol);
            var yesterdayPrice = PrevPrice(priceSymbol);

            var price    = todayPrice?.Price;
            var change   = ComputeChange(todayPrice?.Price, yesterdayPrice?.Price);
            var curValue = price.HasValue ? price.Value * holding.Quantity : (decimal?)null;
            var gainLoss = curValue.HasValue && holding.CostBasis.HasValue && holding.CostBasis.Value != 0
                ? curValue.Value - holding.CostBasis.Value
                : (decimal?)null;
            var gainLossPct = gainLoss.HasValue && holding.CostBasis.HasValue && holding.CostBasis.Value > 0
                ? gainLoss.Value / holding.CostBasis.Value * 100m
                : (decimal?)null;

            results.Add(new TickerItemDto(
                holding.Symbol,
                holding.Name,
                holding.InstrumentType.ToString(),
                price,
                todayPrice?.Currency ?? holding.Currency,
                change?.ChangePercent,
                change?.ChangeAmount,
                Direction: change?.Direction ?? "FLAT",
                IsUserHeld: true,
                curValue,
                gainLoss,
                gainLossPct,
                todayPrice?.AsOfDate.ToString("yyyy-MM-dd")
            ));
        }

        return Ok(results);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetEntityId()
    {
        var claim = User.FindFirstValue("entityId");
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private static PriceChange? ComputeChange(decimal? todayPrice, decimal? yesterdayPrice)
    {
        if (todayPrice == null) return null;
        if (yesterdayPrice == null || yesterdayPrice == 0)
            return new PriceChange(0m, 0m, "FLAT");

        var changeAmount  = todayPrice.Value - yesterdayPrice.Value;
        var changePct     = changeAmount / yesterdayPrice.Value * 100m;
        var direction     = changeAmount > 0 ? "UP" : changeAmount < 0 ? "DOWN" : "FLAT";

        return new PriceChange(Math.Round(changePct, 2), Math.Round(changeAmount, 4), direction);
    }

    // ── Local helper types ────────────────────────────────────────────────────

    private record UserHolding(
        string Symbol,
        string? ExternalSymbol,
        string Name,
        InstrumentType InstrumentType,
        MetalUnit PriceUnit,
        string Currency,
        decimal Quantity,
        decimal? CostBasis);

    private record PriceChange(decimal ChangePercent, decimal ChangeAmount, string Direction);
}

// ── Response DTO ──────────────────────────────────────────────────────────────

public record TickerItemDto(
    string Symbol,
    string Name,
    string Type,
    decimal? Price,
    string Currency,
    decimal? ChangePercent,
    decimal? ChangeAmount,
    string Direction,
    bool IsUserHeld,
    decimal? UserCurrentValue,
    decimal? UserGainLoss,
    decimal? UserGainLossPercent,
    string? AsOfDate
);
