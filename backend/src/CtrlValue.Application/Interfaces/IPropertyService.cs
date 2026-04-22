using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IPropertyService
{
    Task<List<PropertyDto>> GetPropertiesAsync(Guid entityId);
    Task<PropertyDto?> GetPropertyByIdAsync(Guid id, Guid entityId);
    Task<PropertyDto?> GetPropertyByAccountIdAsync(Guid accountId, Guid entityId);
    Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, Guid entityId);
    Task<PropertyDto> UpdatePropertyAsync(Guid id, UpdatePropertyRequest request, Guid entityId);
    Task DeletePropertyAsync(Guid id, Guid entityId);
}
