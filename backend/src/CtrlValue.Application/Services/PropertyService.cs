using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class PropertyService : IPropertyService
{
    private readonly AppDbContext _db;

    public PropertyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PropertyDto>> GetPropertiesAsync(Guid entityId)
    {
        var properties = await _db.Properties
            .Include(p => p.Account)
            .Where(p => p.EntityId == entityId && !p.IsDeleted)
            .OrderBy(p => p.Address)
            .ToListAsync();

        var result = new List<PropertyDto>();
        foreach (var property in properties)
            result.Add(await MapToPropertyDto(property));

        return result;
    }

    public async Task<PropertyDto?> GetPropertyByIdAsync(Guid id, Guid entityId)
    {
        var property = await _db.Properties
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == id && p.EntityId == entityId && !p.IsDeleted);

        return property == null ? null : await MapToPropertyDto(property);
    }

    public async Task<PropertyDto?> GetPropertyByAccountIdAsync(Guid accountId, Guid entityId)
    {
        var property = await _db.Properties
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.AccountId == accountId && p.EntityId == entityId && !p.IsDeleted);

        return property == null ? null : await MapToPropertyDto(property);
    }

    public async Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, Guid entityId)
    {
        // Auto-create the linked ASSET/PROPERTY account
        var account = new Account
        {
            EntityId = entityId,
            Name = request.Address,
            AccountType = AccountType.ASSET,
            AssetClass = AssetClass.PROPERTY,
            LiquidityClass = LiquidityClass.SEMI_LIQUID,
            Currency = request.Currency,
            StartingBalance = request.PurchasePrice,
            StartingBalanceDate = request.PurchaseDate,
            CurrentBalance = request.PurchasePrice,
            TenantId = "default"
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        var property = new Property
        {
            AccountId = account.Id,
            EntityId = entityId,
            Address = request.Address.Trim(),
            Suburb = request.Suburb?.Trim(),
            State = request.State?.Trim(),
            PostCode = request.PostCode?.Trim(),
            Country = request.Country,
            PropertyType = request.PropertyType,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            CarSpaces = request.CarSpaces,
            LandSizeSqm = request.LandSizeSqm,
            FloorSizeSqm = request.FloorSizeSqm,
            YearBuilt = request.YearBuilt,
            PurchasePrice = request.PurchasePrice,
            PurchaseDate = request.PurchaseDate,
            IsRental = request.IsRental,
            WeeklyRentTarget = request.WeeklyRentTarget,
            TenantId = "default"
        };

        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        await _db.Entry(property).Reference(p => p.Account).LoadAsync();

        return await MapToPropertyDto(property);
    }

    public async Task<PropertyDto> UpdatePropertyAsync(Guid id, UpdatePropertyRequest request, Guid entityId)
    {
        var property = await _db.Properties
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == id && p.EntityId == entityId && !p.IsDeleted);

        if (property == null)
            throw new KeyNotFoundException("Property not found or access denied.");

        property.Address = request.Address.Trim();
        property.Suburb = request.Suburb?.Trim();
        property.State = request.State?.Trim();
        property.PostCode = request.PostCode?.Trim();
        property.Country = request.Country;
        property.PropertyType = request.PropertyType;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.CarSpaces = request.CarSpaces;
        property.LandSizeSqm = request.LandSizeSqm;
        property.FloorSizeSqm = request.FloorSizeSqm;
        property.YearBuilt = request.YearBuilt;
        property.IsRental = request.IsRental;
        property.WeeklyRentTarget = request.WeeklyRentTarget;
        property.UpdatedAt = DateTime.UtcNow;

        // Keep the account name in sync with the address
        property.Account.Name = request.Address.Trim();
        property.Account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await MapToPropertyDto(property);
    }

    public async Task DeletePropertyAsync(Guid id, Guid entityId)
    {
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == id && p.EntityId == entityId && !p.IsDeleted);

        if (property == null)
            throw new KeyNotFoundException("Property not found or access denied.");

        property.IsDeleted = true;
        property.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<PropertyDto> MapToPropertyDto(Property property)
    {
        var latestValuation = await _db.Valuations
            .Where(v => v.AccountId == property.AccountId && !v.IsDeleted)
            .OrderByDescending(v => v.AsOfDate)
            .FirstOrDefaultAsync();

        return new PropertyDto
        {
            Id = property.Id,
            AccountId = property.AccountId,
            AccountName = property.Account.Name,
            Address = property.Address,
            Suburb = property.Suburb,
            State = property.State,
            PostCode = property.PostCode,
            Country = property.Country,
            PropertyType = property.PropertyType.ToString(),
            Bedrooms = property.Bedrooms,
            Bathrooms = property.Bathrooms,
            CarSpaces = property.CarSpaces,
            LandSizeSqm = property.LandSizeSqm,
            FloorSizeSqm = property.FloorSizeSqm,
            YearBuilt = property.YearBuilt,
            PurchasePrice = property.PurchasePrice,
            PurchaseDate = property.PurchaseDate,
            IsRental = property.IsRental,
            WeeklyRentTarget = property.WeeklyRentTarget,
            CurrentValue = property.Account.CurrentBalance,
            LatestValuationValue = latestValuation?.Value,
            LatestValuationAsOfDate = latestValuation?.AsOfDate,
            CreatedAt = property.CreatedAt
        };
    }
}
