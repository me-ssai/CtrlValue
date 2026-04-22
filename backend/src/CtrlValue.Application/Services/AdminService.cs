using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.DTOs.Admin;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly IAuditService _audit;
    private readonly IUserDeletionService _userDeletion;

    public AdminService(AppDbContext db, IConfiguration config, IEmailService email, IAuditService audit, IUserDeletionService userDeletion)
    {
        _db            = db;
        _config        = config;
        _email         = email;
        _audit         = audit;
        _userDeletion  = userDeletion;
    }

    // ── Super Admin ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminUserDto>> GetAllUsersAsync()
    {
        var users = await _db.Users
            .Include(u => u.EntityUsers)
                .ThenInclude(eu => eu.Entity)
            .ToListAsync();

        return users.Select(MapToAdminUserDto);
    }

    public async Task UpdateUserRoleAsync(Guid userId, UserRole role)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        user.Role      = role;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.role.changed",
            tenantId: user.TenantId,
            userId: userId,
            detail: $"{{\"role\":\"{role}\"}}");
    }

    public async Task<string> ImpersonateUserAsync(Guid actingAdminId, Guid targetUserId)
    {
        var target = await _db.Users.FindAsync(targetUserId)
            ?? throw new KeyNotFoundException("Target user not found.");

        await _audit.LogAsync("admin.impersonation",
            tenantId: target.TenantId,
            userId: actingAdminId,
            detail: $"{{\"targetUserId\":\"{targetUserId}\",\"targetEmail\":\"{target.Email}\"}}");

        // Generate a short-lived (15 min) JWT as the target with an impersonation marker
        return GenerateImpersonationToken(target, actingAdminId);
    }

    public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync()
    {
        var tenants = await _db.Tenants.ToListAsync();
        return tenants.Select(t => new TenantDto
        {
            Id           = t.Id,
            Name         = t.Name,
            ContactEmail = t.ContactEmail,
            IsActive     = t.IsActive,
            CreatedAt    = t.CreatedAt
        });
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request)
    {
        var tenant = new Tenant
        {
            Name         = request.Name.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            IsActive     = true,
            TenantId     = Guid.NewGuid().ToString() // self-referential TenantId for the tenant record
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("tenant.created",
            tenantId: tenant.TenantId,
            detail: $"{{\"name\":\"{tenant.Name}\",\"contactEmail\":\"{tenant.ContactEmail}\"}}");

        return new TenantDto
        {
            Id           = tenant.Id,
            Name         = tenant.Name,
            ContactEmail = tenant.ContactEmail,
            IsActive     = tenant.IsActive,
            CreatedAt    = tenant.CreatedAt
        };
    }

    public async Task SetTenantActiveAsync(Guid tenantId, bool isActive)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");
        tenant.IsActive  = isActive;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTenantAsync(Guid tenantId, Guid actingAdminId)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId)
            ?? throw new KeyNotFoundException("Tenant not found.");

        // Find all users belonging to this tenant
        var userIds = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenant.TenantId)
            .Select(u => u.Id)
            .ToListAsync();

        await _audit.LogAsync("tenant.deleted",
            tenantId: tenant.TenantId ?? "system",
            userId: actingAdminId,
            objectType: "Tenant",
            objectId: tenantId.ToString(),
            detail: $"{{\"name\":\"{tenant.Name}\",\"userCount\":{userIds.Count}}}");

        // Delete every user in the tenant (and all their owned data)
        foreach (var userId in userIds)
            await _userDeletion.ExecuteUserDeletionAsync(userId, actingAdminId);

        // Hard-delete the tenant record itself
        await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .ExecuteDeleteAsync();
    }

    // ── Site Admin ────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AdminUserDto>> GetTenantUsersAsync(string tenantId)
    {
        var users = await _db.Users
            .Include(u => u.EntityUsers)
                .ThenInclude(eu => eu.Entity)
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        return users.Select(MapToAdminUserDto);
    }

    public async Task InviteUserToEntityAsync(string email, Guid entityId, string tenantId)
    {
        _ = await _db.Entities.FindAsync(entityId)
            ?? throw new KeyNotFoundException("Entity not found.");

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());

        if (existingUser != null)
        {
            // User already exists — link them immediately
            var alreadyLinked = await _db.EntityUsers
                .AnyAsync(eu => eu.UserId == existingUser.Id && eu.EntityId == entityId);

            if (!alreadyLinked)
            {
                var viewerRole = await _db.EntityCustomRoles
                    .FirstOrDefaultAsync(r => r.EntityId == entityId && r.Name == "Viewer" && r.IsSystem);
                if (viewerRole != null)
                {
                    _db.EntityUsers.Add(new EntityUser
                    {
                        UserId       = existingUser.Id,
                        EntityId     = entityId,
                        TenantId     = tenantId,
                        CustomRoleId = viewerRole.Id
                    });
                    await _db.SaveChangesAsync();
                }
            }
        }
        else
        {
            // New user — store a pending invite record
            var placeholder = new User
            {
                Email                 = email.ToLower().Trim(),
                PasswordHash          = string.Empty,
                FirstName             = string.Empty,
                LastName              = string.Empty,
                TenantId              = tenantId,
                IsEmailConfirmed      = false,
                Role                  = UserRole.User,
                InviteToken           = GenerateSecureToken(),
                InviteTokenExpiry     = DateTime.UtcNow.AddHours(48),
                InvitedEntityId       = entityId
            };
            _db.Users.Add(placeholder);
            await _db.SaveChangesAsync();

            await _email.SendInviteEmailAsync(email, placeholder.InviteToken!);
        }

        await _audit.LogAsync("member.invited",
            tenantId: tenantId,
            entityId: entityId,
            detail: $"{{\"email\":\"{email.ToLower()}\"}}");
    }

    public async Task RemoveUserFromEntityAsync(Guid userId, Guid entityId)
    {
        var link = await _db.EntityUsers
            .FirstOrDefaultAsync(eu => eu.UserId == userId && eu.EntityId == entityId)
            ?? throw new KeyNotFoundException("User is not linked to this entity.");

        var tenantId = link.TenantId;
        _db.EntityUsers.Remove(link);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("member.removed",
            tenantId: tenantId,
            userId: userId,
            entityId: entityId);
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AuditLogDto>> GetAuditLogsAsync(
        AuditLogQueryParams q, string callerTenantId, bool isSuperAdmin)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        // SiteAdmin is restricted to their own tenant; SuperAdmin may query any
        if (!isSuperAdmin)
            query = query.Where(a => a.TenantId == callerTenantId);
        else if (!string.IsNullOrWhiteSpace(q.TenantId))
            query = query.Where(a => a.TenantId == q.TenantId);

        if (q.UserId.HasValue)
            query = query.Where(a => a.UserId == q.UserId.Value);
        if (q.EntityId.HasValue)
            query = query.Where(a => a.EntityId == q.EntityId.Value);
        if (!string.IsNullOrWhiteSpace(q.Action))
            query = query.Where(a => a.Action.StartsWith(q.Action));
        if (q.From.HasValue)
            query = query.Where(a => a.Timestamp >= q.From.Value);
        if (q.To.HasValue)
            query = query.Where(a => a.Timestamp <= q.To.Value);

        var pageSize = Math.Clamp(q.PageSize, 1, 200);
        var skip = (Math.Max(q.Page, 1) - 1) * pageSize;

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Enrich with user emails
        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email);

        // Enrich with entity names
        var entityIds = logs
            .Where(l => l.EntityId.HasValue)
            .Select(l => l.EntityId!.Value)
            .Distinct()
            .ToList();

        var entities = entityIds.Count > 0
            ? await _db.Entities
                .Where(e => entityIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name })
                .ToDictionaryAsync(e => e.Id, e => e.Name)
            : new Dictionary<Guid, string>();

        return logs.Select(l => new AuditLogDto(
            l.Id,
            l.TenantId,
            l.UserId,
            users.GetValueOrDefault(l.UserId),
            l.EntityId,
            l.EntityId.HasValue ? entities.GetValueOrDefault(l.EntityId.Value) : null,
            l.Action,
            l.ObjectType,
            l.ObjectId,
            l.Detail,
            l.IpAddress,
            l.Timestamp
        ));
    }

    // ── Deletion Requests ─────────────────────────────────────────────────────

    public async Task<IEnumerable<UserDeletionRequestDto>> GetDeletionRequestsAsync()
    {
        var requests = await _db.DeletionRequests
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(r => new UserDeletionRequestDto
        {
            Id                   = r.Id,
            UserId               = r.UserId,
            UserEmail            = r.User?.Email ?? string.Empty,
            UserFullName         = r.User != null ? $"{r.User.FirstName} {r.User.LastName}".Trim() : string.Empty,
            Status               = r.Status,
            RequestedAt          = r.CreatedAt,
            ScheduledDeletionAt  = r.ScheduledDeletionAt,
            ExpediteRequestedAt  = r.ExpediteRequestedAt,
            ReviewedAt           = r.ReviewedAt,
            RejectionReason      = r.RejectionReason
        });
    }

    public async Task ApproveExpeditedDeletionAsync(Guid requestId, Guid actingUserId)
    {
        var actingUser = await _db.Users.FindAsync(actingUserId)
            ?? throw new KeyNotFoundException("Acting user not found.");

        if (actingUser.Role != UserRole.SuperAdmin && !actingUser.CanApproveDeletions)
            throw new UnauthorizedAccessException("You are not authorized to approve deletion requests.");

        var request = await _db.DeletionRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException("Deletion request not found.");

        if (request.Status != "ExpediteRequested")
            throw new InvalidOperationException($"Request is not in ExpediteRequested state (current: {request.Status}).");

        await _userDeletion.ExecuteUserDeletionAsync(request.UserId, actingUserId);
    }

    public async Task RejectExpeditedDeletionAsync(Guid requestId, Guid actingUserId, string? reason)
    {
        var actingUser = await _db.Users.FindAsync(actingUserId)
            ?? throw new KeyNotFoundException("Acting user not found.");

        if (actingUser.Role != UserRole.SuperAdmin && !actingUser.CanApproveDeletions)
            throw new UnauthorizedAccessException("You are not authorized to reject deletion requests.");

        var request = await _db.DeletionRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException("Deletion request not found.");

        if (request.Status != "ExpediteRequested")
            throw new InvalidOperationException($"Request is not in ExpediteRequested state (current: {request.Status}).");

        // Revert to Pending — the 30-day clock resumes
        request.Status          = "Pending";
        request.ReviewedByUserId = actingUserId;
        request.ReviewedAt      = DateTime.UtcNow;
        request.RejectionReason = reason;
        request.UpdatedAt       = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.expedited_deletion.rejected",
            tenantId: request.TenantId ?? "system",
            userId: actingUserId,
            objectType: "UserDeletionRequest",
            objectId: requestId.ToString(),
            detail: $"{{\"targetUserId\":\"{request.UserId}\",\"reason\":\"{reason}\"}}");
    }

    public async Task SetDeletionApproverAsync(Guid targetUserId, bool canApprove, Guid actingAdminId)
    {
        var target = await _db.Users.FindAsync(targetUserId)
            ?? throw new KeyNotFoundException("User not found.");

        target.CanApproveDeletions = canApprove;
        target.UpdatedAt           = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.deletion_approver.set",
            tenantId: target.TenantId ?? "system",
            userId: actingAdminId,
            objectType: "User",
            objectId: targetUserId.ToString(),
            detail: $"{{\"canApproveDeletions\":{canApprove.ToString().ToLower()}}}");
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    public async Task TriggerPasswordResetAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var token = GenerateSecureToken();
        user.EmailVerificationToken  = token;
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetAsync(user.Email, user.FirstName, token);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private static AdminUserDto MapToAdminUserDto(User u) => new()
    {
        Id        = u.Id,
        Email     = u.Email,
        FirstName = u.FirstName,
        LastName  = u.LastName,
        Role      = u.Role.ToString(),
        TenantId  = u.TenantId,
        Entities  = u.EntityUsers.Select(eu => new EntityMembershipDto
        {
            EntityId   = eu.EntityId,
            EntityName = eu.Entity?.Name ?? string.Empty,
            Role       = eu.CustomRole != null ? eu.CustomRole.Name : string.Empty
        }).ToList()
    };

    private string GenerateImpersonationToken(User target, Guid actingAdminId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, target.Id.ToString()),
            new Claim(ClaimTypes.Email, target.Email),
            new Claim(ClaimTypes.GivenName, target.FirstName),
            new Claim(ClaimTypes.Surname, target.LastName),
            new Claim("tenant_id", target.TenantId),
            new Claim(ClaimTypes.Role, target.Role.ToString()),
            new Claim("impersonating", "true"),
            new Claim("acting_admin_id", actingAdminId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLower();
    }

    private string GetJwtSecret() =>
        _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured.");
}
