using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;
using CtrlValue.Infrastructure.Data.Seeders;

namespace CtrlValue.Application.Services;

public class EntityService : IEntityService
{
    private readonly AppDbContext _db;
    private readonly IDefaultCategorySeeder _categorySeeder;

    public EntityService(AppDbContext db, IDefaultCategorySeeder categorySeeder)
    {
        _db              = db;
        _categorySeeder  = categorySeeder;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity CRUD
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<EntityDto> GetOrCreateDefaultEntityAsync(Guid userId)
    {
        // Return the most recently updated entity where the user has the Owner system role
        var entityUser = await _db.EntityUsers
            .Include(eu => eu.Entity)
            .Include(eu => eu.CustomRole)
            .Where(eu => eu.UserId == userId && eu.CustomRole.IsSystem && eu.CustomRole.Name == "Owner")
            .OrderByDescending(eu => eu.Entity.UpdatedAt)
            .FirstOrDefaultAsync();

        if (entityUser != null)
            return await MapToEntityDto(entityUser.Entity);

        // Create default "Personal" entity for user
        var entity = new Entity { Name = "Personal", BaseCurrency = "AUD", Country = "AU", TenantId = "default" };
        _db.Entities.Add(entity);

        await CreateSystemRolesAsync(entity);
        await _db.SaveChangesAsync();

        var ownerRole = await _db.EntityCustomRoles
            .FirstAsync(r => r.EntityId == entity.Id && r.Name == "Owner");

        _db.EntityUsers.Add(new EntityUser
        {
            EntityId = entity.Id,
            UserId = userId,
            CustomRoleId = ownerRole.Id,
            TenantId = "default"
        });

        await _db.SaveChangesAsync();
        await _categorySeeder.SeedAsync(entity.Id, entity.TenantId);
        return await MapToEntityDto(entity);
    }

    public async Task<List<EntityDto>> GetUserEntitiesAsync(Guid userId)
    {
        var entities = await _db.EntityUsers
            .Include(eu => eu.Entity)
            .ThenInclude(e => e.EntityUsers)
            .ThenInclude(eu => eu.User)
            .Where(eu => eu.UserId == userId && !eu.IsDeleted && !eu.Entity.IsDemo)
            .Select(eu => eu.Entity)
            .ToListAsync();

        var result = new List<EntityDto>();
        foreach (var entity in entities)
            result.Add(await MapToEntityDto(entity));
        return result;
    }

    public async Task<EntityDto?> GetEntityByIdAsync(Guid entityId, Guid userId)
    {
        var hasAccess = await _db.EntityUsers
            .AnyAsync(eu => eu.EntityId == entityId && eu.UserId == userId && !eu.IsDeleted && !eu.Entity.IsDemo);

        if (!hasAccess)
            return null;

        var entity = await _db.Entities
            .Include(e => e.EntityUsers)
            .ThenInclude(eu => eu.User)
            .FirstOrDefaultAsync(e => e.Id == entityId && !e.IsDeleted);

        return entity == null ? null : await MapToEntityDto(entity);
    }

    public async Task<EntityDto> CreateEntityAsync(CreateEntityRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Entity name is required.");

        var entity = new Entity
        {
            Name = request.Name.Trim(),
            BaseCurrency = request.BaseCurrency,
            Country = request.Country,
            TenantId = "default"
        };

        _db.Entities.Add(entity);
        await CreateSystemRolesAsync(entity);
        await _db.SaveChangesAsync();

        var ownerRole = await _db.EntityCustomRoles
            .FirstAsync(r => r.EntityId == entity.Id && r.Name == "Owner");

        _db.EntityUsers.Add(new EntityUser
        {
            EntityId = entity.Id,
            UserId = userId,
            CustomRoleId = ownerRole.Id,
            TenantId = "default"
        });

        await _db.SaveChangesAsync();
        await _categorySeeder.SeedAsync(entity.Id, entity.TenantId);
        return await MapToEntityDto(entity);
    }

    public async Task<EntityDto> UpdateEntityAsync(Guid entityId, UpdateEntityRequest request, Guid userId)
    {
        var entity = await _db.Entities.FindAsync(entityId);
        if (entity == null || entity.IsDeleted)
            throw new KeyNotFoundException("Entity not found.");
        if (entity.IsDemo)
            throw new InvalidOperationException("The demo entity cannot be modified.");

        entity.Name = request.Name.Trim();
        entity.BaseCurrency = request.BaseCurrency;
        entity.Country = request.Country;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await MapToEntityDto(entity);
    }

    public async Task DeleteEntityAsync(Guid entityId, Guid userId)
    {
        var entity = await _db.Entities.FindAsync(entityId);
        if (entity == null || entity.IsDeleted)
            throw new KeyNotFoundException("Entity not found.");
        if (entity.IsDemo)
            throw new InvalidOperationException("The demo entity cannot be deleted.");

        var now = DateTime.UtcNow;

        // 1. Staging rows (children of ImportedTransactionsFiles)
        var importFileIds = await _db.ImportedTransactionsFiles
            .IgnoreQueryFilters()
            .Where(f => f.EntityId == entityId)
            .Select(f => f.Id)
            .ToListAsync();

        if (importFileIds.Count > 0)
        {
            await _db.ImportedTransactionsFilesStaging
                .IgnoreQueryFilters()
                .Where(s => importFileIds.Contains(s.ImportedTransactionsFileId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, now));
        }

        // 2. ImportedTransactionsFiles
        await _db.ImportedTransactionsFiles
            .IgnoreQueryFilters()
            .Where(f => f.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 3. Positions + Valuations + DepreciationSchedules (children of Accounts)
        var accountIds = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.EntityId == entityId)
            .Select(a => a.Id)
            .ToListAsync();

        if (accountIds.Count > 0)
        {
            await _db.Positions
                .IgnoreQueryFilters()
                .Where(p => accountIds.Contains(p.AccountId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, now));

            await _db.Valuations
                .IgnoreQueryFilters()
                .Where(v => accountIds.Contains(v.AccountId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, now));

            await _db.DepreciationSchedules
                .IgnoreQueryFilters()
                .Where(ds => accountIds.Contains(ds.AccountId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsDeleted, true)
                    .SetProperty(x => x.UpdatedAt, now));
        }

        // 4. Transactions
        await _db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 5. Budgets
        await _db.Budgets
            .IgnoreQueryFilters()
            .Where(b => b.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 6. Categories
        await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 7. Accounts
        await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 8. EntityUsers
        await _db.EntityUsers
            .IgnoreQueryFilters()
            .Where(eu => eu.EntityId == entityId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsDeleted, true)
                .SetProperty(x => x.UpdatedAt, now));

        // 9. Entity itself
        entity.IsDeleted = true;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync();
    }


    // ═══════════════════════════════════════════════════════════════════════════
    // EntityUser Management
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<EntityUserDto> AddUserToEntityAsync(Guid entityId, AddEntityUserRequest request, Guid currentUserId)
    {
        var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail);
        if (targetUser == null)
            throw new KeyNotFoundException($"User with email '{request.UserEmail}' not found.");

        var existingAccess = await _db.EntityUsers
            .FirstOrDefaultAsync(eu => eu.EntityId == entityId && eu.UserId == targetUser.Id);

        if (existingAccess != null && !existingAccess.IsDeleted)
            throw new InvalidOperationException("User already has access to this entity.");

        // Validate the role belongs to this entity
        var role = await _db.EntityCustomRoles
            .FirstOrDefaultAsync(r => r.Id == request.CustomRoleId && r.EntityId == entityId && !r.IsDeleted);
        if (role == null)
            throw new KeyNotFoundException("Role not found in this entity.");

        var entityUser = new EntityUser
        {
            EntityId = entityId,
            UserId = targetUser.Id,
            CustomRoleId = request.CustomRoleId,
            TenantId = "default"
        };

        _db.EntityUsers.Add(entityUser);
        await _db.SaveChangesAsync();

        return new EntityUserDto
        {
            Id = entityUser.Id,
            UserId = targetUser.Id,
            UserEmail = targetUser.Email,
            UserName = $"{targetUser.FirstName} {targetUser.LastName}",
            CustomRoleId = entityUser.CustomRoleId,
            RoleName = role.Name,
            CreatedAt = entityUser.CreatedAt
        };
    }

    public async Task<EntityUserDto> UpdateEntityUserRoleAsync(Guid entityId, Guid targetUserId, UpdateEntityUserRequest request, Guid currentUserId)
    {
        if (targetUserId == currentUserId)
            throw new InvalidOperationException("Cannot change your own role.");

        var entityUser = await _db.EntityUsers
            .Include(eu => eu.User)
            .FirstOrDefaultAsync(eu => eu.EntityId == entityId && eu.UserId == targetUserId && !eu.IsDeleted);

        if (entityUser == null)
            throw new KeyNotFoundException("User access not found.");

        // Validate the new role belongs to this entity
        var role = await _db.EntityCustomRoles
            .FirstOrDefaultAsync(r => r.Id == request.CustomRoleId && r.EntityId == entityId && !r.IsDeleted);
        if (role == null)
            throw new KeyNotFoundException("Role not found in this entity.");

        entityUser.CustomRoleId = request.CustomRoleId;
        entityUser.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new EntityUserDto
        {
            Id = entityUser.Id,
            UserId = entityUser.UserId,
            UserEmail = entityUser.User.Email,
            UserName = $"{entityUser.User.FirstName} {entityUser.User.LastName}",
            CustomRoleId = entityUser.CustomRoleId,
            RoleName = role.Name,
            CreatedAt = entityUser.CreatedAt
        };
    }

    public async Task RemoveUserFromEntityAsync(Guid entityId, Guid targetUserId, Guid currentUserId)
    {
        if (targetUserId == currentUserId)
            throw new InvalidOperationException("Cannot remove yourself from the entity.");

        var entityUser = await _db.EntityUsers
            .FirstOrDefaultAsync(eu => eu.EntityId == entityId && eu.UserId == targetUserId && !eu.IsDeleted);

        if (entityUser == null)
            throw new KeyNotFoundException("User access not found.");

        entityUser.IsDeleted = true;
        entityUser.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<EntityUserDto>> GetEntityUsersAsync(Guid entityId, Guid currentUserId)
    {
        var hasAccess = await _db.EntityUsers
            .AnyAsync(eu => eu.EntityId == entityId && eu.UserId == currentUserId && !eu.IsDeleted);

        if (!hasAccess)
            throw new UnauthorizedAccessException("You do not have access to this entity.");

        return await _db.EntityUsers
            .Include(eu => eu.User)
            .Include(eu => eu.CustomRole)
            .Where(eu => eu.EntityId == entityId && !eu.IsDeleted)
            .Select(eu => new EntityUserDto
            {
                Id = eu.Id,
                UserId = eu.UserId,
                UserEmail = eu.User.Email,
                UserName = $"{eu.User.FirstName} {eu.User.LastName}",
                CustomRoleId = eu.CustomRoleId,
                RoleName = eu.CustomRole.Name,
                CreatedAt = eu.CreatedAt
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Custom Role Management
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<EntityCustomRoleDto>> GetEntityRolesAsync(Guid entityId)
    {
        return await _db.EntityCustomRoles
            .Include(r => r.RolePermissions)
            .Where(r => r.EntityId == entityId && !r.IsDeleted)
            .OrderBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new EntityCustomRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                IsSystem = r.IsSystem,
                Permissions = r.RolePermissions.Select(p => p.PermissionKey).ToList()
            })
            .ToListAsync();
    }

    public async Task<EntityCustomRoleDto> CreateEntityRoleAsync(Guid entityId, CreateEntityCustomRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Role name is required.");

        var exists = await _db.EntityCustomRoles
            .AnyAsync(r => r.EntityId == entityId && r.Name == request.Name.Trim() && !r.IsDeleted);
        if (exists)
            throw new InvalidOperationException($"A role named '{request.Name}' already exists in this workspace.");

        var validPerms = new HashSet<string>(Permissions.All);
        var role = new EntityCustomRole
        {
            EntityId = entityId,
            TenantId = "default",
            Name = request.Name.Trim(),
            IsSystem = false
        };

        _db.EntityCustomRoles.Add(role);

        foreach (var perm in request.Permissions.Where(p => validPerms.Contains(p)).Distinct())
            _db.EntityRolePermissions.Add(new EntityRolePermission { RoleId = role.Id, PermissionKey = perm });

        await _db.SaveChangesAsync();

        return new EntityCustomRoleDto
        {
            Id = role.Id,
            Name = role.Name,
            IsSystem = false,
            Permissions = request.Permissions.Where(p => validPerms.Contains(p)).Distinct().ToList()
        };
    }

    public async Task<EntityCustomRoleDto> UpdateEntityRoleAsync(Guid entityId, Guid roleId, UpdateEntityCustomRoleRequest request)
    {
        var role = await _db.EntityCustomRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.EntityId == entityId && !r.IsDeleted);

        if (role == null)
            throw new KeyNotFoundException("Role not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be modified.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Role name is required.");

        var nameConflict = await _db.EntityCustomRoles
            .AnyAsync(r => r.EntityId == entityId && r.Name == request.Name.Trim() && r.Id != roleId && !r.IsDeleted);
        if (nameConflict)
            throw new InvalidOperationException($"A role named '{request.Name}' already exists in this workspace.");

        role.Name = request.Name.Trim();
        role.UpdatedAt = DateTime.UtcNow;

        // Replace permissions
        _db.EntityRolePermissions.RemoveRange(role.RolePermissions);
        var validPerms = new HashSet<string>(Permissions.All);
        foreach (var perm in request.Permissions.Where(p => validPerms.Contains(p)).Distinct())
            _db.EntityRolePermissions.Add(new EntityRolePermission { RoleId = role.Id, PermissionKey = perm });

        await _db.SaveChangesAsync();

        return new EntityCustomRoleDto
        {
            Id = role.Id,
            Name = role.Name,
            IsSystem = false,
            Permissions = request.Permissions.Where(p => validPerms.Contains(p)).Distinct().ToList()
        };
    }

    public async Task DeleteEntityRoleAsync(Guid entityId, Guid roleId)
    {
        var role = await _db.EntityCustomRoles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.EntityId == entityId && !r.IsDeleted);

        if (role == null)
            throw new KeyNotFoundException("Role not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be deleted.");

        var inUse = await _db.EntityUsers.AnyAsync(eu => eu.CustomRoleId == roleId && !eu.IsDeleted);
        if (inUse)
            throw new InvalidOperationException("Cannot delete a role that is currently assigned to users.");

        role.IsDeleted = true;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates the three system roles (Owner, Editor, Viewer) with their default
    /// permission sets for a newly-created entity. Call before SaveChangesAsync.
    /// </summary>
    private async Task CreateSystemRolesAsync(Entity entity)
    {
        var ownerRole   = new EntityCustomRole { EntityId = entity.Id, TenantId = entity.TenantId, Name = "Owner",  IsSystem = true };
        var editorRole  = new EntityCustomRole { EntityId = entity.Id, TenantId = entity.TenantId, Name = "Editor", IsSystem = true };
        var viewerRole  = new EntityCustomRole { EntityId = entity.Id, TenantId = entity.TenantId, Name = "Viewer", IsSystem = true };

        _db.EntityCustomRoles.AddRange(ownerRole, editorRole, viewerRole);

        // Owner gets all permissions
        foreach (var perm in Permissions.All)
            _db.EntityRolePermissions.Add(new EntityRolePermission { RoleId = ownerRole.Id, PermissionKey = perm });

        // Editor: read + write, no manage
        foreach (var perm in new[]
        {
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read,      Permissions.Accounts.Write,
            Permissions.Transactions.Read,  Permissions.Transactions.Write,
            Permissions.Investments.Read,   Permissions.Investments.Write,
            Permissions.Budgets.Read,       Permissions.Budgets.Write,
            Permissions.Reports.Read,
        })
            _db.EntityRolePermissions.Add(new EntityRolePermission { RoleId = editorRole.Id, PermissionKey = perm });

        // Viewer: read only
        foreach (var perm in new[]
        {
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read,
            Permissions.Transactions.Read,
            Permissions.Investments.Read,
            Permissions.Budgets.Read,
            Permissions.Reports.Read,
        })
            _db.EntityRolePermissions.Add(new EntityRolePermission { RoleId = viewerRole.Id, PermissionKey = perm });

        await Task.CompletedTask; // async for consistency; EF tracks changes synchronously
    }

    private async Task<EntityDto> MapToEntityDto(Entity entity)
    {
        var users = await _db.EntityUsers
            .Include(eu => eu.User)
            .Include(eu => eu.CustomRole)
            .Where(eu => eu.EntityId == entity.Id && !eu.IsDeleted)
            .Select(eu => new EntityUserDto
            {
                Id = eu.Id,
                UserId = eu.UserId,
                UserEmail = eu.User.Email,
                UserName = $"{eu.User.FirstName} {eu.User.LastName}",
                CustomRoleId = eu.CustomRoleId,
                RoleName = eu.CustomRole.Name,
                CreatedAt = eu.CreatedAt
            })
            .ToListAsync();

        return new EntityDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BaseCurrency = entity.BaseCurrency,
            Country = entity.Country,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            Users = users
        };
    }
}
