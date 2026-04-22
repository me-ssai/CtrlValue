using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Runs after JWT validation. Loads the authenticated user's Role from the
/// <c>users</c> table and injects it as a standard <see cref="ClaimTypes.Role"/>
/// claim so that [Authorize(Policy = "SuperAdmin")] and
/// [Authorize(Policy = "SiteAdmin")] work correctly.
///
/// Lookup strategy: primary by <c>email</c> claim; fallback by <c>sub</c> UUID.
/// </summary>
public class RoleClaimsTransformer : IClaimsTransformation
{
    private readonly AppDbContext _db;

    public RoleClaimsTransformer(AppDbContext db)
    {
        _db = db;
    }

    // The three valid application roles stored in our users table.
    private static readonly string[] _knownRoles = ["SuperAdmin", "SiteAdmin", "User"];

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only transform authenticated principals.
        if (!principal.Identity?.IsAuthenticated ?? true)
            return principal;

        // Skip if an authoritative application role has already been injected
        // (e.g. on a second call within the same request pipeline).
        if (principal.HasClaim(c => c.Type == ClaimTypes.Role && _knownRoles.Contains(c.Value)))
            return principal;

        var sub   = principal.FindFirstValue("sub");
        var email = principal.FindFirstValue("email")
                 ?? principal.FindFirstValue(ClaimTypes.Email);

        // ── Primary: look up by email ──────────────────────────────────────────
        var user = !string.IsNullOrEmpty(email)
            ? await _db.Users
                .AsNoTracking()
                .Where(u => u.Email == email.ToLower() && !u.IsDeleted)
                .Select(u => new { u.Id, u.Role, u.TenantId })
                .FirstOrDefaultAsync()
            : null;

        // ── Fallback: look up by sub (UUID) ────────────────────────────────────
        if (user == null && Guid.TryParse(sub, out var userId))
        {
            user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.Role, u.TenantId })
                .FirstOrDefaultAsync();
        }

        if (user == null)
            return principal;

        // Clone the identity and inject the role + tenant_id claims
        var clonedIdentity = principal.Identity is ClaimsIdentity existingIdentity
            ? new ClaimsIdentity(existingIdentity)
            : new ClaimsIdentity();

        clonedIdentity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        if (!principal.HasClaim(c => c.Type == "tenant_id"))
            clonedIdentity.AddClaim(new Claim("tenant_id", user.TenantId));

        return new ClaimsPrincipal(clonedIdentity);
    }
}
