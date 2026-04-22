using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Linq;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Domain.Entities;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Application.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly IAuditService _audit;

    public AuthService(AppDbContext db, IConfiguration config, IEmailService email, IAuditService audit)
    {
        _db    = db;
        _config = config;
        _email = email;
        _audit = audit;
    }

    // ── Registration ─────────────────────────────────────────────────────────

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? inviteToken = null)
    {
        // Check if this email is a pending invite placeholder
        var pendingInvite = !string.IsNullOrWhiteSpace(inviteToken)
            ? await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u =>
                    u.Email == request.Email.ToLower() &&
                    u.InviteToken == inviteToken &&
                    u.PasswordHash == string.Empty) // placeholder has no password
            : null;

        if (pendingInvite != null)
        {
            // Invite is valid — check expiry
            if (pendingInvite.InviteTokenExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Your invite link has expired. Please ask the admin to re-invite you.");

            // Upgrade the placeholder into a real user
            pendingInvite.PasswordHash         = BCrypt.Net.BCrypt.HashPassword(request.Password);
            pendingInvite.FirstName            = request.FirstName.Trim();
            pendingInvite.LastName             = request.LastName.Trim();
            pendingInvite.IsEmailConfirmed     = true; // invite = pre-verified email
            pendingInvite.InviteToken          = null;
            pendingInvite.InviteTokenExpiry    = null;

            // Auto-link to the invited entity
            if (pendingInvite.InvitedEntityId.HasValue)
            {
                var viewerRole = await _db.EntityCustomRoles
                    .FirstOrDefaultAsync(r => r.EntityId == pendingInvite.InvitedEntityId.Value
                                           && r.Name == "Viewer" && r.IsSystem);
                if (viewerRole != null)
                {
                    _db.EntityUsers.Add(new Domain.Entities.EntityUser
                    {
                        UserId       = pendingInvite.Id,
                        EntityId     = pendingInvite.InvitedEntityId.Value,
                        TenantId     = pendingInvite.TenantId,
                        CustomRoleId = viewerRole.Id
                    });
                }
                pendingInvite.InvitedEntityId = null;
            }
            await _db.SaveChangesAsync();
            return await GenerateAuthResponse(pendingInvite);
        }

        // Standard registration path
        ValidatePasswordStrength(request.Password);

        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email.ToLower());
        if (exists)
            throw new InvalidOperationException("A user with this email already exists.");

        var verificationToken = Guid.NewGuid().ToString("N"); // 32-char hex

        var user = new User
        {
            Email                    = request.Email.ToLower().Trim(),
            PasswordHash             = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName                = request.FirstName.Trim(),
            LastName                 = request.LastName.Trim(),
            TenantId                 = "default",
            IsEmailConfirmed         = false,
            EmailVerificationToken   = verificationToken,
            EmailVerificationExpiry  = DateTime.UtcNow.AddHours(24)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.registered", tenantId: user.TenantId, userId: user.Id);

        // Send verification email (non-blocking for app startup in dev)
        await _email.SendEmailVerificationAsync(user.Email, user.FirstName, verificationToken, user.TenantId);

        // Return a minimal response — no JWT until verified
        return new AuthResponse
        {
            Token        = string.Empty,
            RefreshToken = string.Empty,
            Expiration   = DateTime.UtcNow,
            RequiresEmailVerification = true,
            User = new UserInfo
            {
                Id        = user.Id,
                Email     = user.Email,
                FirstName = user.FirstName,
                LastName  = user.LastName,
                Role      = user.Role.ToString()
            }
        };
    }

    // ── Email Verification ────────────────────────────────────────────────────

    public async Task VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Verification token is required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        if (user == null)
            throw new InvalidOperationException("Invalid verification token.");

        if (user.EmailVerificationExpiry < DateTime.UtcNow)
        {
            // Token has expired — issue a fresh one and resend the verification email.
            var newToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationToken  = newToken;
            user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
            await _db.SaveChangesAsync();

            await _email.SendEmailVerificationAsync(user.Email, user.FirstName, newToken, user.TenantId);

            throw new InvalidOperationException(
                "Your verification link has expired. A new verification email has been sent to your address.");
        }

        if (user.IsEmailConfirmed)
            return; // Already verified — treat repeat clicks as success

        user.IsEmailConfirmed        = true;
        user.EmailVerificationExpiry = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.email.verified", tenantId: user.TenantId, userId: user.Id);
    }

    public async Task ResendVerificationEmailAsync(string email, User? unverifiedUser = null)
    {
        var user = new User(); 
        if(unverifiedUser != null)
        {
            user = unverifiedUser;
        }
        else
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());
            if (user == null || user.IsEmailConfirmed)
                return; // Don't reveal whether the email exists
        }

        var token = Guid.NewGuid().ToString("N");
        user.EmailVerificationToken  = token;
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
        await _db.SaveChangesAsync();

        await _email.SendEmailVerificationAsync(user.Email, user.FirstName, token, user.TenantId);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

        // User not found — return generic error, no lockout tracking
        if (user == null)
        {
            await _audit.LogAsync("user.login.failed", detail: "{\"reason\":\"user_not_found\"}");
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Check lockout before any further validation
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            await _audit.LogAsync("user.login.failed",
                tenantId: user.TenantId, userId: user.Id,
                detail: "{\"reason\":\"account_locked\"}");
            throw new UnauthorizedAccessException($"Account temporarily locked. Try again in {remaining} minute(s).");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();
                await _audit.LogAsync("user.login.failed",
                    tenantId: user.TenantId, userId: user.Id,
                    detail: $"{{\"reason\":\"locked_after_attempts\",\"attempts\":{user.FailedLoginAttempts}}}");
                throw new UnauthorizedAccessException("Too many failed login attempts. Account locked for 15 minutes.");
            }
            await _db.SaveChangesAsync();
            await _audit.LogAsync("user.login.failed",
                tenantId: user.TenantId, userId: user.Id,
                detail: $"{{\"reason\":\"invalid_password\",\"attempts\":{user.FailedLoginAttempts}}}");
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Successful credential check — reset lockout counters
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        if (!user.IsEmailConfirmed)
        {
            await ResendVerificationEmailAsync(user.Email, user);
            await _audit.LogAsync("user.login.failed",
                tenantId: user.TenantId, userId: user.Id,
                detail: "{\"reason\":\"email_not_verified\"}");
            throw new UnauthorizedAccessException("Please verify your email address before logging in.");
        }

        await _audit.LogAsync("user.login", tenantId: user.TenantId, userId: user.Id);
        return await GenerateAuthResponse(user);
    }

    // ── Session / Logout ─────────────────────────────────────────────────────

    public async Task LogoutAsync(Guid userId, string tenantId)
    {
        await _audit.LogAsync("user.logout", tenantId: tenantId, userId: userId);
    }

    // ── Refresh Token ─────────────────────────────────────────────────────────

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var principal = GetPrincipalFromExpiredToken(request.Token);
        var userId    = principal?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Invalid token.");

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        return await GenerateAuthResponse(user);
    }

    // ── Profile / Password ────────────────────────────────────────────────────

    public async Task<UpdateProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName  = request.LastName.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new UpdateProfileResponse
        {
            User = new UserInfo
            {
                Id                  = user.Id,
                Email               = user.Email,
                FirstName           = user.FirstName,
                LastName            = user.LastName,
                Role                = user.Role.ToString(),
                OnboardingCompleted = user.OnboardingCompletedAt.HasValue
            }
        };
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        ValidatePasswordStrength(request.NewPassword);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt    = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.password.changed", tenantId: user.TenantId, userId: userId);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());
        if (user == null)
            return; // Don't reveal if user exists

        var token = Guid.NewGuid().ToString("N");
        user.PasswordResetToken = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        await _email.SendPasswordResetAsync(user.Email, user.FirstName, token);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new InvalidOperationException("Reset token is required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);
        if (user == null || user.PasswordResetExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired reset token.");

        ValidatePasswordStrength(request.NewPassword);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.password.reset", tenantId: user.TenantId, userId: user.Id);
    }

    // ── Onboarding ────────────────────────────────────────────────────────────

    public async Task CompleteOnboardingAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.OnboardingCompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.onboarding.completed", tenantId: user.TenantId, userId: userId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<AuthResponse> GenerateAuthResponse(User user)
    {
        var token        = GenerateJwtToken(user);
        var refreshToken = GenerateSecureToken();

        user.RefreshToken           = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            Token        = token,
            RefreshToken = refreshToken,
            Expiration   = DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            User = new UserInfo
            {
                Id                  = user.Id,
                Email               = user.Email,
                FirstName           = user.FirstName,
                LastName            = user.LastName,
                Role                = user.Role.ToString(),
                OnboardingCompleted = user.OnboardingCompletedAt.HasValue
            }
        };
    }

    private string GenerateJwtToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim("tenant_id", user.TenantId),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer:            _config["Jwt:Issuer"],
            audience:          _config["Jwt:Audience"],
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience         = true,
            ValidateIssuer           = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret())),
            ValidateLifetime         = false,
            ValidIssuer              = _config["Jwt:Issuer"],
            ValidAudience            = _config["Jwt:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal    = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            throw new SecurityTokenException("Invalid token.");

        return principal;
    }

    // ── Account Deletion ──────────────────────────────────────────────────────

    public async Task RequestAccountDeletionAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var existing = await _db.DeletionRequests
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status != "Completed" && r.Status != "Cancelled");

        if (existing != null)
            throw new InvalidOperationException("A deletion request is already active for this account.");

        var request = new UserDeletionRequest
        {
            UserId              = userId,
            Status              = "Pending",
            ScheduledDeletionAt = DateTime.UtcNow.AddDays(30),
            TenantId            = user.TenantId
        };

        _db.DeletionRequests.Add(request);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.deletion.requested",
            tenantId: user.TenantId ?? "system",
            userId: userId,
            objectType: "UserDeletionRequest",
            objectId: request.Id.ToString());
    }

    public async Task RequestExpeditedDeletionAsync(Guid userId)
    {
        var request = await _db.DeletionRequests
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending")
            ?? throw new InvalidOperationException("No active pending deletion request found. Submit a deletion request first.");

        request.Status               = "ExpediteRequested";
        request.ExpediteRequestedAt  = DateTime.UtcNow;
        request.UpdatedAt            = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.expedited_deletion.requested",
            tenantId: request.TenantId ?? "system",
            userId: userId,
            objectType: "UserDeletionRequest",
            objectId: request.Id.ToString());
    }

    public async Task CancelDeletionRequestAsync(Guid userId)
    {
        var request = await _db.DeletionRequests
            .FirstOrDefaultAsync(r => r.UserId == userId &&
                (r.Status == "Pending" || r.Status == "ExpediteRequested"))
            ?? throw new InvalidOperationException("No cancellable deletion request found.");

        request.Status    = "Cancelled";
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("user.deletion.cancelled",
            tenantId: request.TenantId ?? "system",
            userId: userId,
            objectType: "UserDeletionRequest",
            objectId: request.Id.ToString());
    }

    private string GetJwtSecret()       => _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured.");
    private int    GetTokenExpiryMinutes() => int.TryParse(_config["Jwt:ExpiryMinutes"], out var mins) ? mins : 15;

    private static void ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsLower))
            throw new ArgumentException("Password must contain at least one lowercase letter.");
        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Password must contain at least one digit.");
    }
}
