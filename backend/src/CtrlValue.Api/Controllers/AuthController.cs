using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Infrastructure.Data;
using System.Security.Claims;

namespace CtrlValue.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService authService, AppDbContext db, IWebHostEnvironment env)
    {
        _authService = authService;
        _db          = db;
        _env         = env;
    }

    /// <summary>Returns true when the request originates from the mobile app (React Native).
    /// Mobile clients send X-Mobile-Client: true because they cannot use httpOnly cookies.</summary>
    private bool IsMobileClient() =>
        Request.Headers.TryGetValue("X-Mobile-Client", out var val) && val == "true";

    private void SetAuthCookies(string accessToken, string refreshToken, DateTime accessExpiry)
    {
        // In production the frontend and API are on different domains, so SameSite must be
        // None (with Secure=true) to allow the browser to send the cookie cross-site.
        // In development both run on localhost so Strict is fine and avoids needing HTTPS.
        var isProduction = !_env.IsDevelopment();
        var sameSite     = isProduction ? SameSiteMode.None : SameSiteMode.Strict;

        Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = isProduction,
            SameSite = sameSite,
            Expires  = accessExpiry,
            Path     = "/"
        });

        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = isProduction,
            SameSite = sameSite,
            Expires  = DateTime.UtcNow.AddDays(7),
            Path     = "/api/auth/refresh"   // restrict refresh token to refresh endpoint only
        });
    }

    private void ClearAuthCookies()
    {
        var isProduction = !_env.IsDevelopment();
        var sameSite     = isProduction ? SameSiteMode.None : SameSiteMode.Strict;

        Response.Cookies.Delete("access_token", new CookieOptions
        {
            Secure   = isProduction,
            SameSite = sameSite,
            Path     = "/"
        });
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            Secure   = isProduction,
            SameSite = sameSite,
            Path     = "/api/auth/refresh"
        });
    }

    /// <summary>Register a new user account. Returns RequiresEmailVerification=true — no JWT until verified. Optionally pass ?invite= token to accept an entity invite.</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromQuery] string? invite = null)
    {
        var response = await _authService.RegisterAsync(request, invite);
        return Ok(response);
    }

    /// <summary>Authenticate with email and password. Returns Requires2FA=true + TempToken if 2FA is enabled.</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        if (!string.IsNullOrEmpty(response.Token) && !IsMobileClient())
        {
            SetAuthCookies(response.Token, response.RefreshToken, response.Expiration);
            response.Token        = string.Empty;
            response.RefreshToken = string.Empty;
        }

        return Ok(response);
    }

    /// <summary>Verify email address using the token from the verification email.</summary>
    [HttpGet("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            await _authService.VerifyEmailAsync(token);
            return Ok(new { message = "Email verified successfully. You can now log in." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Returns the current authenticated user's profile with their Role from the database.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            return Unauthorized(new { error = "Invalid user identifier in token." });

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new UserInfo
            {
                Id                  = u.Id,
                Email               = u.Email,
                FirstName           = u.FirstName,
                LastName            = u.LastName,
                IsEmailConfirmed    = u.IsEmailConfirmed,
                Role                = u.Role.ToString(),
                OnboardingCompleted = u.OnboardingCompletedAt != null
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { error = $"User not found in database for id={userId}." });

        return Ok(user);
    }

    /// <summary>Resend the verification email if it expired or was lost.</summary>
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        await _authService.ResendVerificationEmailAsync(request.Email);
        // Always return 200 to avoid email enumeration
        return Ok(new { message = "If that email is registered and unverified, a new verification email has been sent." });
    }

    /// <summary>Refresh an expired JWT. Web clients use httpOnly cookies (no body needed).
    /// Mobile clients (X-Mobile-Client: true) must send { token, refreshToken } in the body
    /// and will receive new tokens in the response body.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? body = null)
    {
        string accessToken;
        string refreshToken;

        if (IsMobileClient())
        {
            accessToken  = body?.Token        ?? string.Empty;
            refreshToken = body?.RefreshToken ?? string.Empty;
        }
        else
        {
            accessToken  = Request.Cookies["access_token"]  ?? string.Empty;
            refreshToken = Request.Cookies["refresh_token"] ?? string.Empty;
        }

        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { error = "No refresh token present." });

        var request  = new RefreshTokenRequest { Token = accessToken, RefreshToken = refreshToken };
        var response = await _authService.RefreshTokenAsync(request);

        if (!IsMobileClient())
        {
            SetAuthCookies(response.Token, response.RefreshToken, response.Expiration);
            response.Token        = string.Empty;
            response.RefreshToken = string.Empty;
        }

        return Ok(response);
    }

    /// <summary>Update user profile (first name, last name).</summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UpdateProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var response = await _authService.UpdateProfileAsync(userId, request);
        return Ok(response);
    }

    /// <summary>Change user password.</summary>
    [Authorize]
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.ChangePasswordAsync(userId, request);
        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>Request a password reset email.</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = "If your email is registered, a password reset link has been sent." });
    }

    /// <summary>Reset password using a token from the email.</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { message = "Password has been reset successfully. You can now log in." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Mark the current user's onboarding as complete.</summary>
    [Authorize]
    [HttpPost("onboarding/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteOnboarding()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.CompleteOnboardingAsync(userId);
        return Ok(new { message = "Onboarding marked as complete." });
    }

    /// <summary>
    /// Records a logout audit event. The actual token invalidation is handled client-side
    /// (tokens cleared from storage). This endpoint enables server-side audit trail of logouts
    /// and is the hook for future token blacklisting.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var userId   = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tenantId = User.FindFirstValue("tenant_id") ?? "system";
        await _authService.LogoutAsync(userId, tenantId);
        ClearAuthCookies();
        return Ok(new { message = "Logged out successfully." });
    }

    // ── Account Deletion ──────────────────────────────────────────────────────

    /// <summary>
    /// Submits a request to permanently delete the authenticated user's account and all
    /// owned data. Deletion is scheduled 30 days from now. The user may cancel within
    /// that window, or request expedited deletion for immediate admin review.
    /// </summary>
    [Authorize]
    [HttpPost("account/deletion-request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestAccountDeletion()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.RequestAccountDeletionAsync(userId);
        return Ok(new { message = "Deletion request submitted. Your account will be permanently deleted in 30 days." });
    }

    /// <summary>
    /// Requests expedited (immediate) deletion. Requires an active pending deletion request.
    /// A SuperAdmin or delegated approver must approve before deletion is executed.
    /// </summary>
    [Authorize]
    [HttpPost("account/deletion-request/expedite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestExpeditedDeletion()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.RequestExpeditedDeletionAsync(userId);
        return Ok(new { message = "Expedited deletion requested. An administrator will review your request." });
    }

    /// <summary>
    /// Cancels an active deletion request (Pending or ExpediteRequested).
    /// No deletion will occur after cancellation.
    /// </summary>
    [Authorize]
    [HttpDelete("account/deletion-request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelDeletionRequest()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _authService.CancelDeletionRequestAsync(userId);
        return Ok(new { message = "Deletion request cancelled." });
    }
}
