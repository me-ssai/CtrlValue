using CtrlValue.Application.DTOs;
using CtrlValue.Application.DTOs.Admin;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Application.Interfaces;

public interface IAdminService
{
    // ── Super Admin ───────────────────────────────────────────────────────────
    Task<IEnumerable<AdminUserDto>> GetAllUsersAsync();
    Task UpdateUserRoleAsync(Guid userId, UserRole role);
    Task<string> ImpersonateUserAsync(Guid actingAdminId, Guid targetUserId);

    Task<IEnumerable<TenantDto>> GetAllTenantsAsync();
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request);
    Task SetTenantActiveAsync(Guid tenantId, bool isActive);
    Task DeleteTenantAsync(Guid tenantId, Guid actingAdminId);

    // ── Site Admin ────────────────────────────────────────────────────────────
    Task<IEnumerable<AdminUserDto>> GetTenantUsersAsync(string tenantId);
    Task InviteUserToEntityAsync(string email, Guid entityId, string tenantId);
    Task RemoveUserFromEntityAsync(Guid userId, Guid entityId);

    // ── Deletion requests ─────────────────────────────────────────────────────
    Task<IEnumerable<UserDeletionRequestDto>> GetDeletionRequestsAsync();
    Task ApproveExpeditedDeletionAsync(Guid requestId, Guid actingUserId);
    Task RejectExpeditedDeletionAsync(Guid requestId, Guid actingUserId, string? reason);
    Task SetDeletionApproverAsync(Guid targetUserId, bool canApprove, Guid actingAdminId);

    // ── Audit ─────────────────────────────────────────────────────────────────
    Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(AuditLogQueryParams queryParams, string callerTenantId, bool isSuperAdmin);

    // ── Shared ────────────────────────────────────────────────────────────────
    Task TriggerPasswordResetAsync(Guid userId);
}
