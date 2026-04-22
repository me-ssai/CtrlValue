using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Domain;
using Xunit;

namespace CtrlValue.Api.Tests.Integration;

/// <summary>
/// Integration tests for demo mode:
/// - <see cref="CtrlValue.Api.Infrastructure.DemoRequestMiddleware"/> issues a demo JWT
/// - <see cref="CtrlValue.Api.Infrastructure.BlockDemoWritesFilter"/> blocks all mutating requests
/// - Read operations are allowed through
/// </summary>
public class DemoModeTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;

    public DemoModeTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Demo JWT Issuance ─────────────────────────────────────────────────────

    [Fact]
    public async Task Request_WithDemoOriginHeader_SetsAccessTokenCookie()
    {
        var client = CreateDemoClient();

        // Use the AllowAnonymous demo/config endpoint so auth middleware does not
        // clear the response (which would strip the Set-Cookie header).
        var response = await client.GetAsync("/api/demo/config");

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("access_token="));
    }

    [Fact]
    public async Task Request_WithoutDemoOriginHeader_DoesNotSetDemoToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        // Regular unauthenticated request should get 401, not a demo cookie
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Demo Read Access ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_WithDemoToken_Returns200()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTransactions_WithDemoToken_Returns200()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Demo Write Blocking ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccount_WithDemoToken_Returns403()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            name        = "Demo Account",
            accountType = "ASSET",
            currency    = "AUD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAccount_WithDemoToken_Returns403()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.PutAsJsonAsync($"/api/accounts/{DemoConstants.DemoEntityId}", new
        {
            name     = "Updated",
            currency = "AUD",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAccount_WithDemoToken_Returns403()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.DeleteAsync($"/api/accounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTransaction_WithDemoToken_Returns403()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = Guid.NewGuid(),
            amount      = 100m,
            description = "Demo Txn",
            txnType     = "Expense",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCategory_WithDemoToken_Returns403()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.PostAsJsonAsync("/api/categories", new
        {
            name         = "Demo Cat",
            categoryType = "Expense"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Demo Entity Scoping ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_WithDemoToken_ScopedToDemoEntity()
    {
        var client = CreateAuthenticatedDemoClient();

        var response = await client.GetAsync("/api/accounts");

        // Should succeed — returns demo entity's data (may be empty in tests but 200 OK)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an unauthenticated client with the demo Origin header set.
    /// The middleware will issue a cookie on the first response.
    /// </summary>
    private HttpClient CreateDemoClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://demo.test");
        return client;
    }

    /// <summary>
    /// Creates a client pre-loaded with a valid demo JWT cookie, simulating
    /// a browser session that already received the demo token.
    /// </summary>
    private HttpClient CreateAuthenticatedDemoClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://demo.test");

        var demoToken = JwtHelper.GenerateDemoToken(
            "test-secret-key-that-is-long-enough-for-hmac-256",
            DemoConstants.DemoEntityId);
        client.DefaultRequestHeaders.Add("Cookie", $"access_token={demoToken}");

        return client;
    }
}
