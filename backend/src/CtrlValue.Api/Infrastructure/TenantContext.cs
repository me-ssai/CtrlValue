using System.Security.Claims;

namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Scoped service that extracts the current user's TenantId, Role, and UserId from JWT claims.
/// Injected into controllers/services to scope queries to the correct tenant.
/// </summary>
public class TenantContext
{
    public string TenantId { get; }
    public string Role { get; }
    public Guid UserId { get; }

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        TenantId = user?.FindFirstValue("tenant_id") ?? string.Empty;
        Role     = user?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        UserId   = Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;
    }
}
