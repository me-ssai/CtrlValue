using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/>.
/// Uses an in-memory EF Core database so no mocking of repositories is needed
/// while keeping tests fast and isolated.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly IConfiguration _config;
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _db = InMemoryDbFactory.Create();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]         = "test-secret-key-that-is-long-enough-for-hmac-256",
                ["Jwt:Issuer"]         = "CtrlValue.Tests",
                ["Jwt:Audience"]       = "CtrlValue.Tests",
                ["Jwt:ExpiryMinutes"]  = "60"
            })
            .Build();

        // Ignore all email/audit calls by default — tests that care will set up specific verifications.
        _emailMock.Setup(e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);
        _emailMock.Setup(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);
        _auditMock.Setup(a => a.LogAsync(
                      It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                      It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                  .Returns(Task.CompletedTask);

        _sut = new AuthService(_db, _config, _emailMock.Object, _auditMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Registration ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CreatesUser()
    {
        var request = ValidRegisterRequest("new@gmail.com");

        var result = await _sut.RegisterAsync(request);

        result.RequiresEmailVerification.Should().BeTrue();
        result.Token.Should().BeEmpty("no JWT until email is verified");
        result.User.Email.Should().Be("new@gmail.com");
        _db.Users.Any(u => u.Email == "new@gmail.com").Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_SetsEmailToLowercase()
    {
        var request = ValidRegisterRequest("Upper@Gmail.COM");

        await _sut.RegisterAsync(request);

        _db.Users.Any(u => u.Email == "upper@gmail.com").Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        await SeedUserAsync("dup@gmail.com");

        var act = () => _sut.RegisterAsync(ValidRegisterRequest("dup@gmail.com"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_SendsVerificationEmail()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("verify@gmail.com"));

        _emailMock.Verify(e => e.SendEmailVerificationAsync(
            "verify@gmail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ThrowsArgumentException()
    {
        var request = new RegisterRequest { Email = "weak@gmail.com", Password = "short", FirstName = "Test", LastName = "User" };

        var act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least 8 characters*");
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordMissingUppercase_ThrowsArgumentException()
    {
        var request = new RegisterRequest { Email = "noup@gmail.com", Password = "password1!", FirstName = "Test", LastName = "User" };

        var act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*uppercase*");
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordMissingDigit_ThrowsArgumentException()
    {
        var request = new RegisterRequest { Email = "nodigit@gmail.com", Password = "PasswordNoDigit", FirstName = "Test", LastName = "User" };

        var act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*digit*");
    }

    [Fact]
    public async Task RegisterAsync_WithValidInviteToken_LinksUserToEntityAsViewer()
    {
        // Arrange: seed a placeholder invite user
        var entity   = new CtrlValue.Domain.Entities.Entity { Id = Guid.NewGuid(), Name = "Test Entity", TenantId = "default" };
        var viewerRole = new EntityCustomRole { Id = Guid.NewGuid(), EntityId = entity.Id, Name = "Viewer", IsSystem = true, TenantId = "default" };
        _db.Entities.Add(entity);
        _db.EntityCustomRoles.Add(viewerRole);

        var inviteToken = "invite-token-123";
        var placeholder = new User
        {
            Email               = "invited@gmail.com",
            PasswordHash        = string.Empty,
            FirstName           = string.Empty,
            LastName            = string.Empty,
            TenantId            = "default",
            InviteToken         = inviteToken,
            InviteTokenExpiry   = DateTime.UtcNow.AddDays(7),
            InvitedEntityId     = entity.Id
        };
        _db.Users.Add(placeholder);
        await _db.SaveChangesAsync();

        var request = ValidRegisterRequest("invited@gmail.com");

        // Act
        var result = await _sut.RegisterAsync(request, inviteToken);

        // Assert
        result.User.Email.Should().Be("invited@gmail.com");
        _db.EntityUsers.Any(eu => eu.UserId == placeholder.Id && eu.EntityId == entity.Id).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_WithExpiredInviteToken_ThrowsInvalidOperationException()
    {
        var inviteToken = "expired-invite";
        _db.Users.Add(new User
        {
            Email             = "expired@gmail.com",
            PasswordHash      = string.Empty,
            FirstName         = string.Empty,
            LastName          = string.Empty,
            TenantId          = "default",
            InviteToken       = inviteToken,
            InviteTokenExpiry = DateTime.UtcNow.AddDays(-1) // expired yesterday
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.RegisterAsync(ValidRegisterRequest("expired@gmail.com"), inviteToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    // ── Email Verification ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_SetsEmailConfirmed()
    {
        var token = "valid-verify-token";
        _db.Users.Add(new User
        {
            Email                   = "unverified@gmail.com",
            PasswordHash            = "hash",
            TenantId                = "default",
            IsEmailConfirmed        = false,
            EmailVerificationToken  = token,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();

        await _sut.VerifyEmailAsync(token);

        var user = _db.Users.First(u => u.Email == "unverified@gmail.com");
        user.IsEmailConfirmed.Should().BeTrue();
        user.EmailVerificationExpiry.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmailAsync_WithExpiredToken_ThrowsAndResendsEmail()
    {
        var token = "expired-verify-token";
        _db.Users.Add(new User
        {
            Email                   = "expiredverify@gmail.com",
            PasswordHash            = "hash",
            FirstName               = "Test",
            TenantId                = "default",
            IsEmailConfirmed        = false,
            EmailVerificationToken  = token,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(-1) // expired
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.VerifyEmailAsync(token);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");

        _emailMock.Verify(e => e.SendEmailVerificationAsync(
            "expiredverify@gmail.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithInvalidToken_ThrowsInvalidOperationException()
    {
        var act = () => _sut.VerifyEmailAsync("nonexistent-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid verification token*");
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyVerified_DoesNotThrow()
    {
        var token = "already-verified-token";
        _db.Users.Add(new User
        {
            Email                   = "alreadyverified@gmail.com",
            PasswordHash            = "hash",
            TenantId                = "default",
            IsEmailConfirmed        = true,
            EmailVerificationToken  = token,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.VerifyEmailAsync(token);

        await act.Should().NotThrowAsync();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        var user = await SeedVerifiedUserAsync("login@gmail.com", "Password1!");

        var result = await _sut.LoginAsync(new LoginRequest { Email = "login@gmail.com", Password = "Password1!" });

        result.Token.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be("login@gmail.com");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedException()
    {
        await SeedVerifiedUserAsync("wrongpw@gmail.com", "Password1!");

        var act = () => _sut.LoginAsync(new LoginRequest { Email = "wrongpw@gmail.com", Password = "WrongPassword1!" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsUnauthorizedException()
    {
        var act = () => _sut.LoginAsync(new LoginRequest { Email = "nobody@gmail.com", Password = "Password1!" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LoginAsync_WithUnverifiedEmail_ThrowsAndResends()
    {
        _db.Users.Add(new User
        {
            Email                   = "unverifiedlogin@gmail.com",
            PasswordHash            = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            FirstName               = "Test",
            TenantId                = "default",
            IsEmailConfirmed        = false,
            EmailVerificationToken  = "token",
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.LoginAsync(new LoginRequest { Email = "unverifiedlogin@gmail.com", Password = "Password1!" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*verify your email*");
    }

    [Fact]
    public async Task LoginAsync_After5FailedAttempts_LocksAccount()
    {
        await SeedVerifiedUserAsync("lockout@gmail.com", "Password1!");

        for (var i = 0; i < 5; i++)
        {
            try { await _sut.LoginAsync(new LoginRequest { Email = "lockout@gmail.com", Password = "Wrong1!" }); }
            catch (UnauthorizedAccessException) { }
        }

        var act = () => _sut.LoginAsync(new LoginRequest { Email = "lockout@gmail.com", Password = "Password1!" });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*locked*");
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetFailedAttempts()
    {
        var user = await SeedVerifiedUserAsync("resetattempts@gmail.com", "Password1!");
        user.FailedLoginAttempts = 3;
        await _db.SaveChangesAsync();

        await _sut.LoginAsync(new LoginRequest { Email = "resetattempts@gmail.com", Password = "Password1!" });

        _db.Users.First(u => u.Email == "resetattempts@gmail.com").FailedLoginAttempts.Should().Be(0);
    }

    // ── Password Reset ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_WithExistingEmail_SendsResetEmail()
    {
        await SeedVerifiedUserAsync("forgotpw@gmail.com", "Password1!");

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "forgotpw@gmail.com" });

        _emailMock.Verify(e => e.SendPasswordResetAsync("forgotpw@gmail.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithUnknownEmail_DoesNotThrow()
    {
        // Anti-enumeration: silently succeeds even for unknown emails
        var act = () => _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "unknown@gmail.com" });

        await act.Should().NotThrowAsync();
        _emailMock.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_UpdatesPasswordHash()
    {
        var token = "reset-token-valid";
        _db.Users.Add(new User
        {
            Email                = "resetpw@gmail.com",
            PasswordHash         = "old-hash",
            TenantId             = "default",
            PasswordResetToken   = token,
            PasswordResetExpiry  = DateTime.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();

        await _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "NewPassword1!" });

        var user = _db.Users.First(u => u.Email == "resetpw@gmail.com");
        BCrypt.Net.BCrypt.Verify("NewPassword1!", user.PasswordHash).Should().BeTrue();
        user.PasswordResetToken.Should().BeNull();
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredToken_ThrowsInvalidOperationException()
    {
        var token = "reset-token-expired";
        _db.Users.Add(new User
        {
            Email               = "expiredreset@gmail.com",
            PasswordHash        = "hash",
            TenantId            = "default",
            PasswordResetToken  = token,
            PasswordResetExpiry = DateTime.UtcNow.AddHours(-1) // expired
        });
        await _db.SaveChangesAsync();

        var act = () => _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "NewPassword1!" });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidToken_ThrowsInvalidOperationException()
    {
        var act = () => _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "bad-token", NewPassword = "NewPassword1!" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Profile / Password ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfileAsync_UpdatesFirstAndLastName()
    {
        var user = await SeedVerifiedUserAsync("profile@gmail.com", "Password1!");

        var result = await _sut.UpdateProfileAsync(user.Id, new UpdateProfileRequest { FirstName = "Updated", LastName = "Name" });

        result.User.FirstName.Should().Be("Updated");
        result.User.LastName.Should().Be("Name");
        _db.Users.Find(user.Id)!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_UpdatesHash()
    {
        var user = await SeedVerifiedUserAsync("changepw@gmail.com", "OldPassword1!");

        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword1!",
            NewPassword     = "NewPassword2!"
        });

        BCrypt.Net.BCrypt.Verify("NewPassword2!", _db.Users.Find(user.Id)!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ThrowsUnauthorizedException()
    {
        var user = await SeedVerifiedUserAsync("wrongcurrent@gmail.com", "Password1!");

        var act = () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword1!",
            NewPassword     = "NewPassword2!"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Deletion Request ─────────────────────────────────────────────────────

    [Fact]
    public async Task RequestAccountDeletionAsync_CreatesPendingRequest()
    {
        var user = await SeedVerifiedUserAsync("deletion@gmail.com", "Password1!");

        await _sut.RequestAccountDeletionAsync(user.Id);

        _db.DeletionRequests.Any(r => r.UserId == user.Id && r.Status == "Pending").Should().BeTrue();
    }

    [Fact]
    public async Task RequestAccountDeletionAsync_WhenRequestAlreadyExists_ThrowsInvalidOperationException()
    {
        var user = await SeedVerifiedUserAsync("deletiondup@gmail.com", "Password1!");
        await _sut.RequestAccountDeletionAsync(user.Id);

        var act = () => _sut.RequestAccountDeletionAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public async Task CancelDeletionRequestAsync_SetsStatusCancelled()
    {
        var user = await SeedVerifiedUserAsync("canceldelete@gmail.com", "Password1!");
        await _sut.RequestAccountDeletionAsync(user.Id);

        await _sut.CancelDeletionRequestAsync(user.Id);

        _db.DeletionRequests.First(r => r.UserId == user.Id).Status.Should().Be("Cancelled");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RegisterRequest ValidRegisterRequest(string email) =>
        new() { Email = email, Password = "Password1!", FirstName = "Test", LastName = "User" };

    private async Task<User> SeedUserAsync(string email)
    {
        var user = new User
        {
            Email            = email.ToLower(),
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            FirstName        = "Seed",
            LastName         = "User",
            TenantId         = "default",
            IsEmailConfirmed = false
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<User> SeedVerifiedUserAsync(string email, string password)
    {
        var user = new User
        {
            Email            = email.ToLower(),
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName        = "Test",
            LastName         = "User",
            TenantId         = "default",
            IsEmailConfirmed = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
