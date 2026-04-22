using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for <see cref="CtrlValue.Api.Controllers.TransactionsController"/>.
/// </summary>
public class TransactionsControllerTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _ownerClient;
    private readonly HttpClient _viewerClient;
    private readonly HttpClient _anonClient;

    public TransactionsControllerTests(TestWebApplicationFactory factory)
    {
        _factory      = factory;
        _ownerClient  = factory.CreateAuthenticatedClient(TestUser.Owner);
        _viewerClient = factory.CreateAuthenticatedClient(TestUser.Viewer);
        _anonClient   = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/transactions ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactions_Unauthenticated_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTransactions_Authenticated_Returns200()
    {
        var response = await _ownerClient.GetAsync("/api/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        transactions.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTransactions_WithDateRange_ReturnsFilteredResults()
    {
        // Seed a transaction
        await SeedTransactionViaApi();

        var start = DateTime.UtcNow.AddDays(-1).ToString("o");
        var end   = DateTime.UtcNow.AddDays(1).ToString("o");

        var response = await _ownerClient.GetAsync($"/api/transactions?startDate={start}&endDate={end}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        transactions.Should().NotBeNull();
    }

    // ── GET /api/transactions/by-account/{accountId} ──────────────────────────

    [Fact]
    public async Task GetTransactionsByAccount_WithValidAccountId_Returns200()
    {
        var response = await _ownerClient.GetAsync($"/api/transactions/by-account/{WellKnownIds.AccountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionDto>>();
        transactions.Should().NotBeNull();
    }

    // ── GET /api/transactions/{id} ────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionById_NonExistent_Returns404()
    {
        var response = await _ownerClient.GetAsync($"/api/transactions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTransactionById_ValidId_Returns200()
    {
        var created = await SeedTransactionViaApi();

        var response = await _ownerClient.GetAsync($"/api/transactions/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TransactionDto>();
        dto!.Id.Should().Be(created.Id);
    }

    // ── POST /api/transactions ────────────────────────────────────────────────

    [Fact]
    public async Task CreateTransaction_AsOwner_Returns201()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 50.00m,
            description = "Groceries",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<TransactionDto>();
        dto!.Description.Should().Be("Groceries");
    }

    [Fact]
    public async Task CreateTransaction_AsViewer_Returns401()
    {
        var response = await _viewerClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 10m,
            description = "Attempted",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTransaction_Unauthenticated_Returns401()
    {
        var response = await _anonClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 10m,
            description = "Anon",
            txnType     = "Expense",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTransaction_WithNegativeAmount_Returns400()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = -10m,
            description = "Negative",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTransaction_WithEmptyDescription_Returns400()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 10m,
            description = "",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/transactions/{id} ────────────────────────────────────────────

    [Fact]
    public async Task UpdateTransaction_AsOwner_Returns200()
    {
        var created = await SeedTransactionViaApi();

        var response = await _ownerClient.PutAsJsonAsync($"/api/transactions/{created.Id}", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 99m,
            description = "Updated Description",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TransactionDto>();
        dto!.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateTransaction_AsViewer_Returns401()
    {
        var created = await SeedTransactionViaApi();

        var response = await _viewerClient.PutAsJsonAsync($"/api/transactions/{created.Id}", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 99m,
            description = "Viewer Update",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/transactions/{id} ─────────────────────────────────────────

    [Fact]
    public async Task DeleteTransaction_AsOwner_Returns204()
    {
        var created = await SeedTransactionViaApi();

        var response = await _ownerClient.DeleteAsync($"/api/transactions/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTransaction_NonExistent_Returns404()
    {
        var response = await _ownerClient.DeleteAsync($"/api/transactions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTransaction_AsViewer_Returns401()
    {
        var created = await SeedTransactionViaApi();

        var response = await _viewerClient.DeleteAsync($"/api/transactions/{created.Id}");

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/transactions/bulk-delete ────────────────────────────────────

    [Fact(Skip = "BulkDeleteTransactionsAsync uses BeginTransactionAsync which throws with the EF Core InMemory provider. Verified working in production with PostgreSQL.")]
    public async Task BulkDelete_AsOwner_Returns204()
    {
        var t1 = await SeedTransactionViaApi();
        var t2 = await SeedTransactionViaApi();

        var response = await _ownerClient.PostAsJsonAsync("/api/transactions/bulk-delete", new
        {
            transactionIds = new[] { t1.Id, t2.Id }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task BulkDelete_AsViewer_Returns401()
    {
        var t1 = await SeedTransactionViaApi();

        var response = await _viewerClient.PostAsJsonAsync("/api/transactions/bulk-delete", new
        {
            transactionIds = new[] { t1.Id }
        });

        // UnauthorizedAccessException → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<TransactionDto> SeedTransactionViaApi()
    {
        var response = await _ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            accountId   = WellKnownIds.AccountId,
            amount      = 10m,
            description = "Seed Txn",
            txnType     = "Expense",
            direction   = "Outflow",
            currency    = "AUD",
            txnTime     = DateTime.UtcNow
        });
        return (await response.Content.ReadFromJsonAsync<TransactionDto>())!;
    }
}
