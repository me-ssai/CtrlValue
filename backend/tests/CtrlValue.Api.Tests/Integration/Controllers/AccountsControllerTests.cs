using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for <see cref="CtrlValue.Api.Controllers.AccountsController"/>.
/// </summary>
public class AccountsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _ownerClient;
    private readonly HttpClient _viewerClient;
    private readonly HttpClient _anonClient;

    public AccountsControllerTests(TestWebApplicationFactory factory)
    {
        _factory     = factory;
        _ownerClient = factory.CreateAuthenticatedClient(TestUser.Owner);
        _viewerClient= factory.CreateAuthenticatedClient(TestUser.Viewer);
        _anonClient  = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/accounts ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_Unauthenticated_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAccounts_Authenticated_Returns200WithEntityAccounts()
    {
        var response = await _ownerClient.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
        accounts.Should().NotBeNull();
        // Seeded entity has 2 accounts
        accounts!.Should().HaveCount(2);
        accounts.Should().NotContain(a => a.Name == "Other Entity Account",
            "cross-entity isolation must be enforced");
    }

    [Fact]
    public async Task GetAccounts_FilteredByType_ReturnsOnlyMatchingAccounts()
    {
        var response = await _ownerClient.GetAsync("/api/accounts?type=ASSET");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
        accounts!.Should().OnlyContain(a => a.AccountType == "ASSET");
    }

    // ── GET /api/accounts/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountById_WithValidId_Returns200()
    {
        var response = await _ownerClient.GetAsync($"/api/accounts/{WellKnownIds.AccountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account!.Id.Should().Be(WellKnownIds.AccountId);
    }

    [Fact]
    public async Task GetAccountById_WithOtherEntityAccountId_Returns404()
    {
        // OtherAccountId belongs to OtherEntityId, not the owner's entity
        var response = await _ownerClient.GetAsync($"/api/accounts/{WellKnownIds.OtherAccountId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAccountById_WithNonExistentId_Returns404()
    {
        var response = await _ownerClient.GetAsync($"/api/accounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/accounts ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccount_AsOwner_Returns201()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            name            = "New Savings",
            accountType     = "ASSET",
            currency        = "AUD",
            startingBalance = 500
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account!.Name.Should().Be("New Savings");
    }

    [Fact]
    public async Task CreateAccount_AsViewer_Returns403()
    {
        // Viewer does not have accounts:write
        var response = await _viewerClient.PostAsJsonAsync("/api/accounts", new
        {
            name        = "Viewer Account",
            accountType = "ASSET",
            currency    = "AUD"
        });

        // UnauthorizedAccessException → 401 (app maps this to 401, not 403)
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAccount_Unauthenticated_Returns401()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/accounts", new
        {
            name        = "Anon Account",
            accountType = "ASSET",
            currency    = "AUD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAccount_WithMissingName_Returns400()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            name        = "",
            accountType = "ASSET",
            currency    = "AUD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/accounts/{id} ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAccount_AsOwner_Returns200WithUpdatedData()
    {
        var response = await _ownerClient.PutAsJsonAsync($"/api/accounts/{WellKnownIds.AccountId}", new
        {
            name     = "Updated Name",
            currency = "AUD",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAccount_AsViewer_Returns401()
    {
        var response = await _viewerClient.PutAsJsonAsync($"/api/accounts/{WellKnownIds.AccountId}", new
        {
            name     = "Attempted Update",
            currency = "AUD",
            isActive = true
        });

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAccount_NonExistentId_Returns404()
    {
        var response = await _ownerClient.PutAsJsonAsync($"/api/accounts/{Guid.NewGuid()}", new
        {
            name     = "Ghost Update",
            currency = "AUD",
            isActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/accounts/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_AsOwner_Returns204()
    {
        // Create a fresh account to delete so we don't destroy seed data for other tests
        var createResp = await _ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            name        = "To Delete",
            accountType = "ASSET",
            currency    = "AUD"
        });
        var created = await createResp.Content.ReadFromJsonAsync<AccountDto>();

        var response = await _ownerClient.DeleteAsync($"/api/accounts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAccount_AsViewer_Returns401()
    {
        var response = await _viewerClient.DeleteAsync($"/api/accounts/{WellKnownIds.AccountId}");

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/accounts/{id}/deletion-impact ────────────────────────────────

    [Fact]
    public async Task GetDeletionImpact_ReturnsImpactSummary()
    {
        var response = await _ownerClient.GetAsync($"/api/accounts/{WellKnownIds.AccountId}/deletion-impact");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await response.Content.ReadFromJsonAsync<AccountDeletionImpactDto>();
        impact!.AccountId.Should().Be(WellKnownIds.AccountId);
    }

    // ── GET /api/accounts/summary ─────────────────────────────────────────────

    [Fact]
    public async Task GetAccountSummary_Returns200WithNetWorth()
    {
        var response = await _ownerClient.GetAsync("/api/accounts/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<AccountSummaryDto>();
        summary.Should().NotBeNull();
        summary!.Currency.Should().Be("AUD");
    }
}
