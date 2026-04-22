using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class InstrumentService : IInstrumentService
{
    private readonly AppDbContext _db;

    public InstrumentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<InstrumentDto>> GetInstrumentsAsync(InstrumentType? type = null)
    {
        var query = _db.Instruments.Where(i => !i.IsDeleted);

        if (type.HasValue)
            query = query.Where(i => i.InstrumentType == type.Value);

        var instruments = await query
            .OrderBy(i => i.Symbol)
            .ToListAsync();

        var result = new List<InstrumentDto>();
        foreach (var instrument in instruments)
        {
            var dto = await MapToInstrumentDto(instrument);
            result.Add(dto);
        }

        return result;
    }

    public async Task<InstrumentDto?> GetInstrumentByIdAsync(Guid id)
    {
        var instrument = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        return instrument == null ? null : await MapToInstrumentDto(instrument);
    }

    public async Task<InstrumentDto?> GetInstrumentBySymbolAsync(string symbol)
    {
        var instrument = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol && !i.IsDeleted);

        return instrument == null ? null : await MapToInstrumentDto(instrument);
    }

    public async Task<InstrumentDto> CreateInstrumentAsync(CreateInstrumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Instrument symbol is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Instrument name is required.");

        // Check for duplicate symbol
        var existing = await _db.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == request.Symbol.ToUpper() && !i.IsDeleted);

        if (existing != null)
            throw new InvalidOperationException($"Instrument with symbol '{request.Symbol}' already exists.");

        var instrument = new Instrument
        {
            Symbol = request.Symbol.ToUpper().Trim(),
            Name = request.Name.Trim(),
            InstrumentType = request.InstrumentType,
            Currency = request.Currency,
            Exchange = request.Exchange?.Trim(),
            ExternalSymbol = request.ExternalSymbol?.Trim(),
            PriceProvider = request.PriceProvider,
            PriceUnit = request.PriceUnit,
            TenantId = "default",
            // Bond fields
            Issuer = request.Issuer?.Trim(),
            FaceValue = request.FaceValue,
            CouponRate = request.CouponRate,
            CouponFrequency = request.CouponFrequency?.Trim(),
            MaturityDate = request.MaturityDate,
            IssueDate = request.IssueDate,
            CreditRating = request.CreditRating?.Trim().ToUpper(),
            // ETF / Fund fields
            ExpenseRatio = request.ExpenseRatio,
            DistributionYield = request.DistributionYield,
            DistributionFrequency = request.DistributionFrequency?.Trim(),
            UnderlyingIndex = request.UnderlyingIndex?.Trim()
        };

        _db.Instruments.Add(instrument);
        await _db.SaveChangesAsync();

        return await MapToInstrumentDto(instrument);
    }

    public async Task<InstrumentDto> UpdateInstrumentAsync(Guid id, UpdateInstrumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Instrument name is required.");

        var instrument = await _db.Instruments.FindAsync(id);
        if (instrument == null || instrument.IsDeleted)
            throw new KeyNotFoundException("Instrument not found.");

        instrument.Name = request.Name.Trim();
        instrument.Currency = request.Currency;
        instrument.Exchange = request.Exchange?.Trim();
        instrument.ExternalSymbol = request.ExternalSymbol?.Trim();
        instrument.PriceProvider = request.PriceProvider;
        instrument.PriceUnit = request.PriceUnit;
        // Bond fields
        instrument.Issuer = request.Issuer?.Trim();
        instrument.FaceValue = request.FaceValue;
        instrument.CouponRate = request.CouponRate;
        instrument.CouponFrequency = request.CouponFrequency?.Trim();
        instrument.MaturityDate = request.MaturityDate;
        instrument.IssueDate = request.IssueDate;
        instrument.CreditRating = request.CreditRating?.Trim().ToUpper();
        // ETF / Fund fields
        instrument.ExpenseRatio = request.ExpenseRatio;
        instrument.DistributionYield = request.DistributionYield;
        instrument.DistributionFrequency = request.DistributionFrequency?.Trim();
        instrument.UnderlyingIndex = request.UnderlyingIndex?.Trim();
        instrument.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await MapToInstrumentDto(instrument);
    }

    public async Task DeleteInstrumentAsync(Guid id)
    {
        var instrument = await _db.Instruments.FindAsync(id);
        if (instrument == null || instrument.IsDeleted)
            throw new KeyNotFoundException("Instrument not found.");

        // Check if instrument is used in any positions
        var hasPositions = await _db.Positions
            .AnyAsync(p => p.InstrumentId == id && !p.IsDeleted);

        if (hasPositions)
            throw new InvalidOperationException("Cannot delete instrument that is used in positions.");

        // Soft delete
        instrument.IsDeleted = true;
        instrument.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<InstrumentDto> MapToInstrumentDto(Instrument instrument)
    {
        // 1. Manual price history (always AUD, user-entered)
        var manual = await _db.PriceHistory
            .Where(ph => ph.InstrumentId == instrument.Id && !ph.IsDeleted)
            .OrderByDescending(ph => ph.AsOfDate)
            .FirstOrDefaultAsync();

        // 2. Global cache (API-sourced, AUD only — same resolution logic as PositionService)
        var cached = await _db.GlobalPriceCache
            .Where(g => (g.Symbol == instrument.ExternalSymbol || g.Symbol == instrument.Symbol)
                     && string.Equals(g.Currency, "AUD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g => g.AsOfDate)
            .FirstOrDefaultAsync();

        // Use whichever source has the more recent date
        decimal? latestPriceValue = null;
        DateTime? latestPriceDate = null;
        if (manual != null && (cached == null || manual.AsOfDate >= cached.AsOfDate))
        {
            latestPriceValue = manual.ClosePrice;
            latestPriceDate  = manual.AsOfDate;
        }
        else if (cached != null)
        {
            latestPriceValue = cached.Price;
            latestPriceDate  = cached.AsOfDate;
        }

        return new InstrumentDto
        {
            Id = instrument.Id,
            Symbol = instrument.Symbol,
            Name = instrument.Name,
            InstrumentType = instrument.InstrumentType.ToString(),
            Currency = instrument.Currency,
            Exchange = instrument.Exchange,
            ExternalSymbol = instrument.ExternalSymbol,
            PriceProvider = instrument.PriceProvider?.ToString(),
            PriceUnit = instrument.PriceUnit.ToString(),
            LatestPrice = latestPriceValue,
            LatestPriceDate = latestPriceDate,
            CreatedAt = instrument.CreatedAt,
            // Bond fields
            Issuer = instrument.Issuer,
            FaceValue = instrument.FaceValue,
            CouponRate = instrument.CouponRate,
            CouponFrequency = instrument.CouponFrequency,
            MaturityDate = instrument.MaturityDate,
            IssueDate = instrument.IssueDate,
            CreditRating = instrument.CreditRating,
            // ETF / Fund fields
            ExpenseRatio = instrument.ExpenseRatio,
            DistributionYield = instrument.DistributionYield,
            DistributionFrequency = instrument.DistributionFrequency,
            UnderlyingIndex = instrument.UnderlyingIndex
        };
    }
}
