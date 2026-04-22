using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Api.Tests.Infrastructure;

/// <summary>
/// Generates signed JWT tokens for use in integration tests without going through
/// the real AuthController login flow.
/// </summary>
public static class JwtHelper
{
    private const string Issuer   = "CtrlValue.Tests";
    private const string Audience = "CtrlValue.Tests";

    public static string GenerateToken(TestUser user, string secret)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.GivenName,      user.FirstName),
            new(ClaimTypes.Surname,        user.LastName),
            new("tenant_id",               "default"),
            new(ClaimTypes.Role,           user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Generates a demo JWT (role=demo, demo_session=true).</summary>
    public static string GenerateDemoToken(string secret, Guid entityId)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "demo"),
            new Claim("entity_id",    entityId.ToString()),
            new Claim("demo_session", "true"),
            new Claim("session_id",   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Represents a test user identity used to create authenticated test clients.
/// </summary>
public class TestUser
{
    public Guid     UserId    { get; init; } = Guid.NewGuid();
    public string   Email     { get; init; } = "test@test.com";
    public string   FirstName { get; init; } = "Test";
    public string   LastName  { get; init; } = "User";
    public UserRole Role      { get; init; } = UserRole.User;
    public Guid?    EntityId  { get; init; }

    // Pre-built convenience identities
    public static readonly TestUser Owner = new()
    {
        UserId   = WellKnownIds.OwnerUserId,
        Email    = "owner@test.com",
        Role     = UserRole.User,
        EntityId = WellKnownIds.EntityId
    };

    public static readonly TestUser Editor = new()
    {
        UserId   = WellKnownIds.EditorUserId,
        Email    = "editor@test.com",
        Role     = UserRole.User,
        EntityId = WellKnownIds.EntityId
    };

    public static readonly TestUser Viewer = new()
    {
        UserId   = WellKnownIds.ViewerUserId,
        Email    = "viewer@test.com",
        Role     = UserRole.User,
        EntityId = WellKnownIds.EntityId
    };

    public static readonly TestUser SiteAdmin = new()
    {
        UserId   = WellKnownIds.SiteAdminUserId,
        Email    = "admin@test.com",
        Role     = UserRole.SiteAdmin,
        EntityId = WellKnownIds.EntityId
    };

    public static readonly TestUser SuperAdmin = new()
    {
        UserId   = WellKnownIds.SuperAdminUserId,
        Email    = "super@test.com",
        Role     = UserRole.SuperAdmin,
        EntityId = WellKnownIds.EntityId
    };
}
