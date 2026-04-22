using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Domain.Utilities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class PositionService : IPositionService
{
    private readonly AppDbContext _db;

    public PositionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PositionDto>> GetPositionsAsync(Guid entityId, Guid? accountId = null)
    {
        var query = _db.Positions
            .Include(p => p.Account)
            .Include(p => p.Instrument)
            .Where(p => p.Account.EntityId == entityId && !p.IsDeleted);

        if (accountId.HasValue)
            query = query.Where(p => p.AccountId == accountId.Value);

        var positions = await query
            .OrderByDescending(p => p.OpenedAt)
            .ToListAsync();

        var result = new List<PositionDto>();
        foreach (var position in positions)
        {
            var dto = await MapToPositionDto(position);
            result.Add(dto);
        }

        return result;
    }

    public async Task<PositionDto?> GetPositionByIdAsync(Guid id, Guid entityId)
    {
        var position = await _db.Positions
            .Include(p => p.Account)
            .Include(p => p.Instrument)
            .Where(p => p.Id == id && p.Account.EntityId == entityId && !p.IsDeleted)
            .FirstOrDefaultAsync();

        return position == null ? null : await MapToPositionDto(position);
    }

    public async Task<PositionDto> CreatePositionAsync(CreatePositionRequest request, Guid entityId)
    {
        // Verify account belongs to entity
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId && !a.IsDeleted);

        if (account == null)
            throw new KeyNotFoundException("Account not found or access denied.");

        // Verify instrument exists if provided
        if (request.InstrumentId.HasValue)
        {
            var instrument = await _db.Instruments
                .FirstOrDefaultAsync(i => i.Id == request.InstrumentId.Value && !i.IsDeleted);

            if (instrument == null)
                throw new KeyNotFoundException("Instrument not found.");
        }

        var position = new Position
        {
            AccountId = request.AccountId,
            InstrumentId = request.InstrumentId,
            Quantity = request.Quantity,
            Unit = request.Unit,
            CostBasisTotal = request.CostBasisTotal,
            OpenedAt = request.OpenedAt ?? DateTime.UtcNow,
            TenantId = "default",
        };

        _db.Positions.Add(position);
        await _db.SaveChangesAsync();

        // Reload with navigation properties
        await _db.Entry(position).Reference(p => p.Account).LoadAsync();
        await _db.Entry(position).Reference(p => p.Instrument).LoadAsync();

        return await MapToPositionDto(position);
    }

    public async Task<PositionDto> UpdatePositionAsync(Guid id, UpdatePositionRequest request, Guid entityId)
    {
        var position = await _db.Positions
            .Include(p => p.Account)
            .Include(p => p.Instrument)
            .Where(p => p.Id == id && p.Account.EntityId == entityId && !p.IsDeleted)
            .FirstOrDefaultAsync();

        if (position == null)
            throw new KeyNotFoundException("Position not found or access denied.");

        position.Quantity = request.Quantity;
        position.CostBasisTotal = request.CostBasisTotal;
        position.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await MapToPositionDto(position);
    }

    public async Task DeletePositionAsync(Guid id, Guid entityId)
    {
        var position = await _db.Positions
            .Include(p => p.Account)
            .Where(p => p.Id == id && p.Account.EntityId == entityId && !p.IsDeleted)
            .FirstOrDefaultAsync();

        if (position == null)
            throw new KeyNotFoundException("Position not found or access denied.");

        // Soft delete
        position.IsDeleted = true;
        position.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<decimal?> GetPositionValueAsync(Guid id, Guid entityId)
    {
        var position = await _db.Positions
            .Include(p => p.Account)
            .Include(p => p.Instrument)
            .Where(p => p.Id == id && p.Account.EntityId == entityId && !p.IsDeleted)
            .FirstOrDefaultAsync();

        if (position == null)
            return null;

        if (position.Instrument == null)
            return position.CostBasisTotal;

        var price = await GetLatestPriceAsync(position.Instrument);
        if (price == null)
            return position.CostBasisTotal;

        var convertedQty = MetalUnitConverter.Convert(position.Quantity, position.Unit, position.Instrument.PriceUnit);
        return convertedQty * price.Value;
    }

    public async Task<PositionPerformanceDto> GetPositionPerformanceAsync(Guid id, Guid entityId)
    {
        var position = await _db.Positions
            .Include(p => p.Account)
            .Include(p => p.Instrument)
            .Where(p => p.Id == id && p.Account.EntityId == entityId && !p.IsDeleted)
            .FirstOrDefaultAsync();

        if (position == null)
            throw new KeyNotFoundException("Position not found or access denied.");

        decimal? currentPrice = null;
        decimal? currentValue = null;

        if (position.Instrument != null)
        {
            currentPrice = await GetLatestPriceAsync(position.Instrument);
            if (currentPrice.HasValue)
            {
                var convertedQty = MetalUnitConverter.Convert(position.Quantity, position.Unit, position.Instrument.PriceUnit);
                currentValue = convertedQty * currentPrice.Value;
            }
        }

        currentValue ??= position.CostBasisTotal;
        var costBasis = position.CostBasisTotal;
        var averageCostPerUnit = position.Quantity != 0 ? costBasis / position.Quantity : null;

        decimal? unrealizedGainLoss = null;
        decimal? unrealizedGainLossPercent = null;

        // When cost basis is null or zero, use currentValue as effective basis → 0 gain/loss
        // rather than showing a misleading full-value "gain"
        var effectiveBasis = (costBasis.HasValue && costBasis.Value != 0) ? costBasis : currentValue;
        if (currentValue.HasValue && effectiveBasis.HasValue)
        {
            unrealizedGainLoss = currentValue.Value - effectiveBasis.Value;
            unrealizedGainLossPercent = effectiveBasis.Value != 0
                ? (unrealizedGainLoss.Value / effectiveBasis.Value) * 100
                : 0m;
        }

        return new PositionPerformanceDto
        {
            CurrentValue = currentValue,
            CostBasis = costBasis,
            UnrealizedGainLoss = unrealizedGainLoss,
            UnrealizedGainLossPercent = unrealizedGainLossPercent,
            CurrentPrice = currentPrice,
            AverageCostPerUnit = averageCostPerUnit
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<PositionDto> MapToPositionDto(Position position)
    {
        decimal? currentPrice = null;
        decimal? currentValue = null;

        if (position.Instrument != null)
        {
            currentPrice = await GetLatestPriceAsync(position.Instrument);
            if (currentPrice.HasValue)
            {
                // Convert position quantity to the instrument's price unit before multiplying
                var convertedQty = MetalUnitConverter.Convert(position.Quantity, position.Unit, position.Instrument.PriceUnit);
                currentValue = convertedQty * currentPrice.Value;
            }
        }

        currentValue ??= position.CostBasisTotal;
        var costBasisPerUnit = position.Quantity != 0 ? position.CostBasisTotal / position.Quantity : null;

        decimal? unrealizedGainLoss = null;
        decimal? unrealizedGainLossPercent = null;

        // When cost basis is null or zero, use currentValue as effective basis → 0 gain/loss
        var effectiveBasis = (position.CostBasisTotal.HasValue && position.CostBasisTotal.Value != 0)
            ? position.CostBasisTotal
            : currentValue;
        if (currentValue.HasValue && effectiveBasis.HasValue)
        {
            unrealizedGainLoss = currentValue.Value - effectiveBasis.Value;
            unrealizedGainLossPercent = effectiveBasis.Value != 0
                ? (unrealizedGainLoss.Value / effectiveBasis.Value) * 100
                : 0m;
        }

        return new PositionDto
        {
            Id = position.Id,
            AccountId = position.AccountId,
            AccountName = position.Account.Name,
            InstrumentId = position.InstrumentId,
            InstrumentSymbol = position.Instrument?.Symbol,
            InstrumentName = position.Instrument?.Name,
            InstrumentType = position.Instrument?.InstrumentType.ToString(),
            Quantity = position.Quantity,
            Unit = position.Unit.ToString(),
            CostBasisTotal = position.CostBasisTotal,
            CostBasisPerUnit = costBasisPerUnit,
            CurrentPrice = currentPrice,
            CurrentValue = currentValue,
            UnrealizedGainLoss = unrealizedGainLoss,
            UnrealizedGainLossPercent = unrealizedGainLossPercent,
            Currency = position.Account.Currency,
            OpenedAt = position.OpenedAt,
            CreatedAt = position.CreatedAt
        };
    }

    /// <summary>
    /// Returns the latest price for an instrument in AUD.
    /// Checks GlobalPriceCache first (populated by the background price-fetch job),
    /// then falls back to manually entered PriceHistory records.
    /// Prices stored as USD (non-ASX stocks) are skipped in favour of manual entries.
    /// </summary>
    private async Task<decimal?> GetLatestPriceAsync(Instrument instrument)
    {
        // 1. Global cache (cross-tenant, API-sourced)
        var cached = await _db.GlobalPriceCache
            .Where(g => g.Symbol == instrument.ExternalSymbol || g.Symbol == instrument.Symbol)
            .OrderByDescending(g => g.AsOfDate)
            .FirstOrDefaultAsync();

        // Only use cached price if it is quoted in AUD.
        // USD-priced entries (e.g. US-listed stocks via Alpha Vantage) are skipped
        // so they fall back to the user's manually entered PriceHistory in AUD.
        if (cached != null && string.Equals(cached.Currency, "AUD", StringComparison.OrdinalIgnoreCase))
            return cached.Price;

        // 2. Manual per-instrument price history (always in AUD — entered by the user)
        var manual = await _db.PriceHistory
            .Where(ph => ph.InstrumentId == instrument.Id && !ph.IsDeleted)
            .OrderByDescending(ph => ph.AsOfDate)
            .FirstOrDefaultAsync();

        return manual?.ClosePrice;
    }
}
