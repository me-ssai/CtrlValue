using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CtrlValue.Domain;

namespace CtrlValue.Api.Infrastructure;

/// <summary>
/// Inspects the Origin request header and sets <see cref="DemoRequestContext.IsDemo"/> = true
/// when the request originates from a configured demo origin (e.g. https://demo.ctrlvalue.com).
///
/// The Origin header is set automatically by browsers for all cross-origin requests and cannot
/// be overridden by client-side JavaScript. This makes it a reliable server-side gate.
///
/// When IsDemo is true, this middleware also issues a short-lived anonymous demo JWT cookie
/// so that the demo frontend can make authenticated-looking API calls without any real user.
/// </summary>
public class DemoRequestMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _allowedOrigins;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _tokenExpiryMinutes;

    public DemoRequestMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;

        _allowedOrigins = config.GetSection("Demo:AllowedOrigins").Get<string[]>()
                       ?? config["Demo:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       ?? Array.Empty<string>();

        _jwtSecret         = config["Jwt:Secret"] ?? string.Empty;
        _jwtIssuer         = config["Jwt:Issuer"] ?? string.Empty;
        _jwtAudience       = config["Jwt:Audience"] ?? string.Empty;
        _tokenExpiryMinutes = config.GetValue<int>("Demo:TokenExpiryMinutes", 60);
    }

    public async Task InvokeAsync(HttpContext context, DemoRequestContext demoCtx)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault() ?? string.Empty;

        bool isDemo = _allowedOrigins.Any(o =>
            string.Equals(o, origin, StringComparison.OrdinalIgnoreCase));

        if (!isDemo)
        {
            await _next(context);
            return;
        }

        demoCtx.IsDemo = true;

        // Issue a fresh demo session cookie if none exists or the existing one is not a demo token
        if (!HasValidDemoToken(context))
        {
            var token = IssueDemoToken();
            context.Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure   = true,
                SameSite = SameSiteMode.None,
                Expires  = DateTimeOffset.UtcNow.AddMinutes(_tokenExpiryMinutes),
                Path     = "/"
            });
        }

        await _next(context);
    }

    private bool HasValidDemoToken(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue("access_token", out var token))
            return false;

        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = _jwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = _jwtAudience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero,
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;
            return jwt.Claims.Any(c => c.Type == "demo_session" && c.Value == "true");
        }
        catch
        {
            return false;
        }
    }

    private string IssueDemoToken()
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, DemoConstants.DemoRole),
            new Claim("entity_id",    DemoConstants.DemoEntityId.ToString()),
            new Claim("demo_session", "true"),
            new Claim("session_id",   Guid.NewGuid().ToString()),
        };

        var jwt = new JwtSecurityToken(
            issuer:             _jwtIssuer,
            audience:           _jwtAudience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_tokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
