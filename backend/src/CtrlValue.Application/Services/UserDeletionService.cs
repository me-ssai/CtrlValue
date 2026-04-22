using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.Interfaces;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

/// <summary>
/// Executes a full, permanent hard-delete of all data owned by a user.
/// Uses ExecuteDeleteAsync so the EF Core soft-delete interceptor is bypassed entirely.
/// The caller is responsible for verifying authorization before invoking this service.
/// </summary>
public class UserDeletionService : IUserDeletionService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public UserDeletionService(AppDbContext db, IAuditService audit)
    {
        _db    = db;
        _audit = audit;
    }

    public async Task ExecuteUserDeletionAsync(Guid userId, Guid actingUserId)
    {
        // Load user details (IgnoreQueryFilters in case already soft-deleted somehow)
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        var tenantId = user.TenantId ?? "system";

        // ── Write audit entry BEFORE any rows are removed ──
        await _audit.LogAsync(
            "user.account.deleted",
            tenantId: tenantId,
            userId: actingUserId,
            objectType: "User",
            objectId: userId.ToString(),
            detail: $"{{\"deletedUserId\":\"{userId}\",\"email\":\"{user.Email}\"}}");

        // ── Find all entities this user owns ──
        var ownedEntityIds = await _db.EntityUsers
            .IgnoreQueryFilters()
            .Where(eu => eu.UserId == userId)
            .Join(
                _db.EntityCustomRoles.IgnoreQueryFilters().Where(r => r.Name == "Owner" && r.IsSystem),
                eu => eu.CustomRoleId,
                r  => r.Id,
                (eu, _) => eu.EntityId)
            .ToListAsync();

        foreach (var entityId in ownedEntityIds)
            await HardDeleteEntityAsync(entityId);

        // ── Remove any remaining EntityUser memberships (non-owner) ──
        await _db.EntityUsers
            .IgnoreQueryFilters()
            .Where(eu => eu.UserId == userId)
            .ExecuteDeleteAsync();

        // ── Mark the DeletionRequest as Completed (if one exists) ──
        await _db.DeletionRequests
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId && (r.Status == "Pending" || r.Status == "ExpediteRequested"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, "Completed")
                .SetProperty(r => r.ReviewedByUserId, actingUserId)
                .SetProperty(r => r.ReviewedAt, DateTime.UtcNow));

        // ── Hard-delete the User row ──
        await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers — delete one entity and all its nested data
    // ─────────────────────────────────────────────────────────────────────────

    private async Task HardDeleteEntityAsync(Guid entityId)
    {
        // 1. Import staging rows (children of ImportedTransactionsFiles)
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
                .ExecuteDeleteAsync();
        }

        // 2. Import files
        await _db.ImportedTransactionsFiles
            .IgnoreQueryFilters()
            .Where(f => f.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 3. Account-level children (Positions, Valuations, DepreciationSchedules,
        //    LoanRateHistory, LoanDetails, Properties)
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
                .ExecuteDeleteAsync();

            await _db.Valuations
                .IgnoreQueryFilters()
                .Where(v => accountIds.Contains(v.AccountId))
                .ExecuteDeleteAsync();

            await _db.DepreciationSchedules
                .IgnoreQueryFilters()
                .Where(ds => accountIds.Contains(ds.AccountId))
                .ExecuteDeleteAsync();

            // LoanRateHistory → via LoanDetails → Account
            var loanDetailIds = await _db.LoanDetails
                .IgnoreQueryFilters()
                .Where(l => accountIds.Contains(l.AccountId))
                .Select(l => l.Id)
                .ToListAsync();

            if (loanDetailIds.Count > 0)
            {
                await _db.LoanRateHistory
                    .IgnoreQueryFilters()
                    .Where(r => loanDetailIds.Contains(r.LoanDetailsId))
                    .ExecuteDeleteAsync();
            }

            await _db.LoanDetails
                .IgnoreQueryFilters()
                .Where(l => accountIds.Contains(l.AccountId))
                .ExecuteDeleteAsync();

            await _db.Properties
                .IgnoreQueryFilters()
                .Where(p => accountIds.Contains(p.AccountId))
                .ExecuteDeleteAsync();
        }

        // 4. Transactions
        await _db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 5. Budgets
        await _db.Budgets
            .IgnoreQueryFilters()
            .Where(b => b.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 6. CategoryKeywordRules
        await _db.CategoryKeywordRules
            .IgnoreQueryFilters()
            .Where(r => r.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 7. Categories — children (sub-categories) before parents
        //    Sub-categories have a non-null ParentCategoryId.
        await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.EntityId == entityId && c.ParentCategoryId != null)
            .ExecuteDeleteAsync();

        await _db.Categories
            .IgnoreQueryFilters()
            .Where(c => c.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 8. Accounts
        await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 9. EntityUsers — must come before EntityCustomRoles due to FK RESTRICT
        await _db.EntityUsers
            .IgnoreQueryFilters()
            .Where(eu => eu.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 10. EntityRolePermissions (via custom roles)
        var roleIds = await _db.EntityCustomRoles
            .IgnoreQueryFilters()
            .Where(r => r.EntityId == entityId)
            .Select(r => r.Id)
            .ToListAsync();

        if (roleIds.Count > 0)
        {
            await _db.EntityRolePermissions
                .IgnoreQueryFilters()
                .Where(p => roleIds.Contains(p.RoleId))
                .ExecuteDeleteAsync();
        }

        // 11. EntityCustomRoles
        await _db.EntityCustomRoles
            .IgnoreQueryFilters()
            .Where(r => r.EntityId == entityId)
            .ExecuteDeleteAsync();

        // 12. Entity itself
        await _db.Entities
            .IgnoreQueryFilters()
            .Where(e => e.Id == entityId)
            .ExecuteDeleteAsync();
    }
}
