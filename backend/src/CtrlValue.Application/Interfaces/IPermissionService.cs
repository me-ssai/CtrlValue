namespace CtrlValue.Application.Interfaces;

public interface IPermissionService
{
    /// <summary>
    /// Returns true if the user holds the given permission within the entity.
    /// SuperAdmin and SiteAdmin bypass entity-level permission checks.
    /// </summary>
    Task<bool> HasAsync(Guid userId, Guid entityId, string permission);

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the user does not hold the permission.
    /// </summary>
    Task RequireAsync(Guid userId, Guid entityId, string permission);
}
