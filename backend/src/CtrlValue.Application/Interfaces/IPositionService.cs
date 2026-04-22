using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IPositionService
{
    // Position CRUD
    Task<List<PositionDto>> GetPositionsAsync(Guid entityId, Guid? accountId = null);
    Task<PositionDto?> GetPositionByIdAsync(Guid id, Guid entityId);
    Task<PositionDto> CreatePositionAsync(CreatePositionRequest request, Guid entityId);
    Task<PositionDto> UpdatePositionAsync(Guid id, UpdatePositionRequest request, Guid entityId);
    Task DeletePositionAsync(Guid id, Guid entityId);
    
    // Position calculations
    Task<decimal?> GetPositionValueAsync(Guid id, Guid entityId);
    Task<PositionPerformanceDto> GetPositionPerformanceAsync(Guid id, Guid entityId);
}

public class PositionPerformanceDto
{
    public decimal? CurrentValue { get; set; }
    public decimal? CostBasis { get; set; }
    public decimal? UnrealizedGainLoss { get; set; }
    public decimal? UnrealizedGainLossPercent { get; set; }
    public decimal? CurrentPrice { get; set; }
    public decimal? AverageCostPerUnit { get; set; }
}
