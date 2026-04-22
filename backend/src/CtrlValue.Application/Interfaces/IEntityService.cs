using CtrlValue.Application.DTOs;

namespace CtrlValue.Application.Interfaces;

public interface IEntityService
{
    // Entity CRUD
    Task<EntityDto> GetOrCreateDefaultEntityAsync(Guid userId);
    Task<List<EntityDto>> GetUserEntitiesAsync(Guid userId);
    Task<EntityDto?> GetEntityByIdAsync(Guid entityId, Guid userId);
    Task<EntityDto> CreateEntityAsync(CreateEntityRequest request, Guid userId);
    Task<EntityDto> UpdateEntityAsync(Guid entityId, UpdateEntityRequest request, Guid userId);
    Task DeleteEntityAsync(Guid entityId, Guid userId);
    
    // EntityUser Management
    Task<EntityUserDto> AddUserToEntityAsync(Guid entityId, AddEntityUserRequest request, Guid currentUserId);
    Task<EntityUserDto> UpdateEntityUserRoleAsync(Guid entityId, Guid targetUserId, UpdateEntityUserRequest request, Guid currentUserId);
    Task RemoveUserFromEntityAsync(Guid entityId, Guid targetUserId, Guid currentUserId);
    Task<List<EntityUserDto>> GetEntityUsersAsync(Guid entityId, Guid currentUserId);

    // Custom Role Management
    Task<List<EntityCustomRoleDto>> GetEntityRolesAsync(Guid entityId);
    Task<EntityCustomRoleDto> CreateEntityRoleAsync(Guid entityId, CreateEntityCustomRoleRequest request);
    Task<EntityCustomRoleDto> UpdateEntityRoleAsync(Guid entityId, Guid roleId, UpdateEntityCustomRoleRequest request);
    Task DeleteEntityRoleAsync(Guid entityId, Guid roleId);
}
