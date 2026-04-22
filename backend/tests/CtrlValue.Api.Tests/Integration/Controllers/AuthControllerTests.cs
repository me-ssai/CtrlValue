using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using Xunit;

namespace CtrlValue.Api.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for <see cref="CtrlValue.Api.Controllers.AuthController"/>.
/// Tests the full HTTP pipeline including middleware, serialisation, and cookies.
/// </summary>
public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidGmailPayload_Returns200WithVerificationFlag()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "newuser@gmail.com",
            password  = "Password1!",
            firstName = "New",
            lastName  = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.RequiresEmailVerification.Should().BeTrue();
        body.Token.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WithNonGmailEmail_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "user@hotmail.com",
            password  = "Password1!",
            firstName = "Test",
            lastName  = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        // First registration succeeds
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "dup@gmail.com",
            password  = "Password1!",
            firstName = "Dup",
            lastName  = "User"
        });

        // Second attempt should fail
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "dup@gmail.com",
            password  = "Password1!",
            firstName = "Dup",
            lastName  = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email     = "weakpw@gmail.com",
            password  = "weak",
            firstName = "Test",
            lastName  = "User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndSetsAuthCookie()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "owner@test.com",
            password = "Password1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Cookie should be set (web client, no X-Mobile-Client header)
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith("access_token="));
        // Token must be redacted from body for web clients
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().BeEmpty("token is moved to cookie for web clients");
    }

    [Fact]
    public async Task Login_WithMobileClientHeader_Returns200WithTokenInBody()
    {
        _client.DefaultRequestHeaders.Add("X-Mobile-Client", "true");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "owner@test.com",
            password = "Password1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeEmpty("mobile clients get token in body");

        _client.DefaultRequestHeaders.Remove("X-Mobile-Client");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Wrong credentials → UnauthorizedAccessException → 401
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "owner@test.com",
            password = "WrongPassword1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        // Unknown email → UnauthorizedAccessException → 401
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email    = "nobody@gmail.com",
            password = "Password1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Get Current User ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_Authenticated_Returns200WithUserInfo()
    {
        var authenticatedClient = _factory.CreateAuthenticatedClient(TestUser.Owner);

        var response = await authenticatedClient.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserInfo>();
        body!.Email.Should().Be("owner@test.com");
    }

    // ── Email Verification ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_Returns400()
    {
        var response = await _client.GetAsync("/api/auth/verify-email?token=bad-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Resend Verification ───────────────────────────────────────────────────

    [Fact]
    public async Task ResendVerification_AlwaysReturns200_RegardlessOfEmail()
    {
        // Anti-enumeration: should not reveal whether email exists
        var response = await _client.PostAsJsonAsync("/api/auth/resend-verification", new
        {
            email = "unknown@gmail.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Forgot / Reset Password ───────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AlwaysReturns200_RegardlessOfEmail()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            email = "doesnotexist@gmail.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token       = "invalid-token",
            newPassword = "NewPassword1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Authenticated_Returns200AndClearsCookies()
    {
        var authenticatedClient = _factory.CreateAuthenticatedClient(TestUser.Owner);

        var response = await authenticatedClient.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Profile / Password (authenticated) ───────────────────────────────────

    [Fact]
    public async Task UpdateProfile_Authenticated_Returns200WithUpdatedInfo()
    {
        var authenticatedClient = _factory.CreateAuthenticatedClient(TestUser.Owner);

        var response = await authenticatedClient.PutAsJsonAsync("/api/auth/profile", new
        {
            firstName = "Updated",
            lastName  = "Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateProfileResponse>();
        body!.User.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        var response = await _client.PutAsJsonAsync("/api/auth/profile", new
        {
            firstName = "Updated",
            lastName  = "Name"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401()
    {
        // Wrong current password → UnauthorizedAccessException → 401
        // Endpoint: PUT /api/auth/password
        var authenticatedClient = _factory.CreateAuthenticatedClient(TestUser.Owner);

        var response = await authenticatedClient.PutAsJsonAsync("/api/auth/password", new
        {
            currentPassword = "WrongPassword1!",
            newPassword     = "NewPassword2!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
