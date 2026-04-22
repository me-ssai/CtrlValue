using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasAsync(Guid userId, Guid entityId, string permission)
    {
        // SuperAdmin / SiteAdmin bypass entity-level permission checks entirely.
        var userRole = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();

        if (userRole == UserRole.SuperAdmin || userRole == UserRole.SiteAdmin)
            return true;

        // Check if the user's custom role for this entity grants the requested permission.
        return await _db.EntityUsers
            .AsNoTracking()
            .Where(eu => eu.UserId == userId && eu.EntityId == entityId && !eu.IsDeleted)
            .SelectMany(eu => eu.CustomRole.RolePermissions)
            .AnyAsync(p => p.PermissionKey == permission);
    }

    public async Task RequireAsync(Guid userId, Guid entityId, string permission)
    {
        if (!await HasAsync(userId, entityId, permission))
            throw new UnauthorizedAccessException(
                $"You do not have the '{permission}' permission in this workspace.");
    }
}
