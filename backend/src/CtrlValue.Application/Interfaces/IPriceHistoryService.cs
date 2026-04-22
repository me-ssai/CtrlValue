using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IPriceHistoryService
{
    Task<List<PriceHistoryDto>> GetPriceHistoryAsync(Guid instrumentId, DateTime? startDate = null, DateTime? endDate = null);
    Task<PriceHistoryDto?> GetLatestPriceAsync(Guid instrumentId);
    Task<PriceHistoryDto> CreatePriceHistoryAsync(CreatePriceHistoryRequest request);
    Task<int> BulkImportPricesAsync(BulkPriceImportRequest request);
    Task DeletePriceHistoryAsync(Guid id);
}

public interface IValuationService
{
    Task<List<ValuationDto>> GetValuationsAsync(Guid entityId, Guid? accountId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<ValuationDto?> GetLatestValuationAsync(Guid accountId, Guid entityId);
    Task<ValuationDto> CreateValuationAsync(CreateValuationRequest request, Guid entityId);
    Task<ValuationDto> UpdateValuationAsync(Guid id, UpdateValuationRequest request, Guid entityId);
    Task DeleteValuationAsync(Guid id, Guid entityId);
}

public interface IDepreciationScheduleService
{
    Task<List<DepreciationScheduleDto>> GetDepreciationSchedulesAsync(Guid entityId);
    Task<DepreciationScheduleDto?> GetDepreciationScheduleByIdAsync(Guid id, Guid entityId);
    Task<DepreciationScheduleDto?> GetDepreciationScheduleByAccountAsync(Guid accountId, Guid entityId);
    Task<DepreciationScheduleDto> CreateDepreciationScheduleAsync(CreateDepreciationScheduleRequest request, Guid entityId);
    Task<DepreciationScheduleDto> UpdateDepreciationScheduleAsync(Guid id, UpdateDepreciationScheduleRequest request, Guid entityId);
    Task DeleteDepreciationScheduleAsync(Guid id, Guid entityId);
    Task<decimal> CalculateCurrentValueAsync(Guid id, Guid entityId, DateTime? asOfDate = null);
}

public interface IBudgetService
{
    Task<List<BudgetDto>> GetBudgetsAsync(Guid entityId, Guid? categoryId = null);
    Task<BudgetDto?> GetBudgetByIdAsync(Guid id, Guid entityId);
    Task<BudgetDto> CreateBudgetAsync(CreateBudgetRequest request, Guid entityId);
    Task<BudgetDto> UpdateBudgetAsync(Guid id, UpdateBudgetRequest request, Guid entityId);
    Task DeleteBudgetAsync(Guid id, Guid entityId);
    Task<List<BudgetDto>> GetActiveBudgetsAsync(Guid entityId, DateTime? asOfDate = null);
}
