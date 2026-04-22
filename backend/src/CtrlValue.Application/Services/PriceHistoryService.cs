using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class PriceHistoryService : IPriceHistoryService
{
    private readonly AppDbContext _db;

    public PriceHistoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PriceHistoryDto>> GetPriceHistoryAsync(Guid instrumentId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.PriceHistory
            .Include(ph => ph.Instrument)
            .Where(ph => ph.InstrumentId == instrumentId && !ph.IsDeleted);

        if (startDate.HasValue)
            query = query.Where(ph => ph.AsOfDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(ph => ph.AsOfDate <= endDate.Value);

        return await query
            .OrderByDescending(ph => ph.AsOfDate)
            .Select(ph => new PriceHistoryDto
            {
                Id = ph.Id,
                InstrumentId = ph.InstrumentId,
                InstrumentSymbol = ph.Instrument.Symbol,
                AsOfDate = ph.AsOfDate,
                OpenPrice = ph.OpenPrice,
                ClosePrice = ph.ClosePrice,
                HighPrice = ph.HighPrice,
                LowPrice = ph.LowPrice,
                Volume = ph.Volume,
                Currency = ph.Currency,
                Source = ph.Source,
                CreatedAt = ph.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<PriceHistoryDto?> GetLatestPriceAsync(Guid instrumentId)
    {
        var price = await _db.PriceHistory
            .Include(ph => ph.Instrument)
            .Where(ph => ph.InstrumentId == instrumentId && !ph.IsDeleted)
            .OrderByDescending(ph => ph.AsOfDate)
            .FirstOrDefaultAsync();

        if (price == null)
            return null;

        return new PriceHistoryDto
        {
            Id = price.Id,
            InstrumentId = price.InstrumentId,
            InstrumentSymbol = price.Instrument.Symbol,
            AsOfDate = price.AsOfDate,
            OpenPrice = price.OpenPrice,
            ClosePrice = price.ClosePrice,
            HighPrice = price.HighPrice,
            LowPrice = price.LowPrice,
            Volume = price.Volume,
            Currency = price.Currency,
            Source = price.Source,
            CreatedAt = price.CreatedAt
        };
    }

    public async Task<PriceHistoryDto> CreatePriceHistoryAsync(CreatePriceHistoryRequest request)
    {
        var instrument = await _db.Instruments.FindAsync(request.InstrumentId);
        if (instrument == null || instrument.IsDeleted)
            throw new KeyNotFoundException("Instrument not found.");

        var priceHistory = new PriceHistory
        {
            InstrumentId = request.InstrumentId,
            AsOfDate = request.AsOfDate,
            OpenPrice = request.OpenPrice,
            ClosePrice = request.ClosePrice,
            HighPrice = request.HighPrice,
            LowPrice = request.LowPrice,
            Volume = request.Volume,
            Currency = request.Currency,
            Source = request.Source,
            TenantId = "default"
        };

        _db.PriceHistory.Add(priceHistory);
        await _db.SaveChangesAsync();

        await _db.Entry(priceHistory).Reference(ph => ph.Instrument).LoadAsync();

        return new PriceHistoryDto
        {
            Id = priceHistory.Id,
            InstrumentId = priceHistory.InstrumentId,
            InstrumentSymbol = priceHistory.Instrument.Symbol,
            AsOfDate = priceHistory.AsOfDate,
            OpenPrice = priceHistory.OpenPrice,
            ClosePrice = priceHistory.ClosePrice,
            HighPrice = priceHistory.HighPrice,
            LowPrice = priceHistory.LowPrice,
            Volume = priceHistory.Volume,
            Currency = priceHistory.Currency,
            Source = priceHistory.Source,
            CreatedAt = priceHistory.CreatedAt
        };
    }

    public async Task<int> BulkImportPricesAsync(BulkPriceImportRequest request)
    {
        var instrument = await _db.Instruments.FindAsync(request.InstrumentId);
        if (instrument == null || instrument.IsDeleted)
            throw new KeyNotFoundException("Instrument not found.");

        var priceHistories = new List<PriceHistory>();

        foreach (var price in request.Prices)
        {
            // Check if price already exists for this date
            var exists = await _db.PriceHistory
                .AnyAsync(ph => ph.InstrumentId == request.InstrumentId 
                    && ph.AsOfDate.Date == price.Date.Date 
                    && !ph.IsDeleted);

            if (!exists)
            {
                priceHistories.Add(new PriceHistory
                {
                    InstrumentId = request.InstrumentId,
                    AsOfDate = price.Date,
                    OpenPrice = price.Open,
                    ClosePrice = price.Price,
                    HighPrice = price.High,
                    LowPrice = price.Low,
                    Volume = price.Volume,
                    Currency = instrument.Currency,
                    Source = "Bulk Import",
                    TenantId = "default"
                });
            }
        }

        if (priceHistories.Any())
        {
            _db.PriceHistory.AddRange(priceHistories);
            await _db.SaveChangesAsync();
        }

        return priceHistories.Count;
    }

    public async Task DeletePriceHistoryAsync(Guid id)
    {
        var priceHistory = await _db.PriceHistory.FindAsync(id);
        if (priceHistory == null || priceHistory.IsDeleted)
            throw new KeyNotFoundException("Price history not found.");

        priceHistory.IsDeleted = true;
        priceHistory.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}

public class ValuationService : IValuationService
{
    private readonly AppDbContext _db;

    public ValuationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ValuationDto>> GetValuationsAsync(Guid entityId, Guid? accountId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.Valuations
            .Include(v => v.Account)
            .Where(v => v.Account.EntityId == entityId && !v.IsDeleted);

        if (accountId.HasValue)
            query = query.Where(v => v.AccountId == accountId.Value);

        if (startDate.HasValue)
            query = query.Where(v => v.AsOfDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(v => v.AsOfDate <= endDate.Value);

        return await query
            .OrderByDescending(v => v.AsOfDate)
            .Select(v => new ValuationDto
            {
                Id = v.Id,
                AccountId = v.AccountId,
                AccountName = v.Account.Name,
                AsOfDate = v.AsOfDate,
                Value = v.Value,
                Currency = v.Currency,
                Source = v.Source,
                Notes = v.Notes,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ValuationDto?> GetLatestValuationAsync(Guid accountId, Guid entityId)
    {
        var valuation = await _db.Valuations
            .Include(v => v.Account)
            .Where(v => v.AccountId == accountId && v.Account.EntityId == entityId && !v.IsDeleted)
            .OrderByDescending(v => v.AsOfDate)
            .FirstOrDefaultAsync();

        if (valuation == null)
            return null;

        return new ValuationDto
        {
            Id = valuation.Id,
            AccountId = valuation.AccountId,
            AccountName = valuation.Account.Name,
            AsOfDate = valuation.AsOfDate,
            Value = valuation.Value,
            Currency = valuation.Currency,
            Source = valuation.Source,
            Notes = valuation.Notes,
            CreatedAt = valuation.CreatedAt
        };
    }

    public async Task<ValuationDto> CreateValuationAsync(CreateValuationRequest request, Guid entityId)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.EntityId == entityId && !a.IsDeleted);

        if (account == null)
            throw new KeyNotFoundException("Account not found or access denied.");

        var valuation = new Valuation
        {
            AccountId = request.AccountId,
            AsOfDate = request.AsOfDate,
            Value = request.Value,
            Currency = request.Currency,
            Source = request.Source,
            Notes = request.Notes,
            TenantId = "default"
        };

        _db.Valuations.Add(valuation);
        await _db.SaveChangesAsync();

        await _db.Entry(valuation).Reference(v => v.Account).LoadAsync();

        return new ValuationDto
        {
            Id = valuation.Id,
            AccountId = valuation.AccountId,
            AccountName = valuation.Account.Name,
            AsOfDate = valuation.AsOfDate,
            Value = valuation.Value,
            Currency = valuation.Currency,
            Source = valuation.Source,
            Notes = valuation.Notes,
            CreatedAt = valuation.CreatedAt
        };
    }

    public async Task<ValuationDto> UpdateValuationAsync(Guid id, UpdateValuationRequest request, Guid entityId)
    {
        var valuation = await _db.Valuations
            .Include(v => v.Account)
            .Where(v => v.Id == id && v.Account.EntityId == entityId && !v.IsDeleted)
            .FirstOrDefaultAsync();

        if (valuation == null)
            throw new KeyNotFoundException("Valuation not found or access denied.");

        valuation.Value = request.Value;
        if (request.AsOfDate.HasValue) valuation.AsOfDate = request.AsOfDate.Value;
        if (request.Notes != null)     valuation.Notes = request.Notes;
        if (request.Source != null)    valuation.Source = request.Source;
        valuation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new ValuationDto
        {
            Id = valuation.Id,
            AccountId = valuation.AccountId,
            AccountName = valuation.Account.Name,
            AsOfDate = valuation.AsOfDate,
            Value = valuation.Value,
            Currency = valuation.Currency,
            Source = valuation.Source,
            Notes = valuation.Notes,
            CreatedAt = valuation.CreatedAt
        };
    }

    public async Task DeleteValuationAsync(Guid id, Guid entityId)
    {
        var valuation = await _db.Valuations
            .Include(v => v.Account)
            .Where(v => v.Id == id && v.Account.EntityId == entityId && !v.IsDeleted)
            .FirstOrDefaultAsync();

        if (valuation == null)
            throw new KeyNotFoundException("Valuation not found or access denied.");

        valuation.IsDeleted = true;
        valuation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}
