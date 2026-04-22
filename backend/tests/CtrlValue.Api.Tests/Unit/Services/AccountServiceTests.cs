using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="AccountService"/>.
/// </summary>
public class AccountServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly AccountService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;
    private readonly Guid _otherEntityId = WellKnownIds.OtherEntityId;

    public AccountServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new AccountService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetAccountsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountsAsync_ReturnsAccountsForEntityOnly()
    {
        SeedAccount(_entityId, "My Account");
        SeedAccount(_otherEntityId, "Other Entity Account");

        var result = await _sut.GetAccountsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("My Account");
    }

    [Fact]
    public async Task GetAccountsAsync_ExcludesSoftDeletedAccounts()
    {
        SeedAccount(_entityId, "Active");
        SeedAccount(_entityId, "Deleted", isDeleted: true);

        var result = await _sut.GetAccountsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Active");
    }

    [Fact]
    public async Task GetAccountsAsync_FilteredByType_ReturnsOnlyMatchingAccounts()
    {
        SeedAccount(_entityId, "Checking", AccountType.ASSET);
        SeedAccount(_entityId, "Credit Card", AccountType.LIABILITY);

        var result = await _sut.GetAccountsAsync(_entityId, AccountType.ASSET);

        result.Should().HaveCount(1);
        result[0].AccountType.Should().Be("ASSET");
    }

    [Fact]
    public async Task GetAccountsAsync_WithNoAccounts_ReturnsEmptyList()
    {
        var result = await _sut.GetAccountsAsync(_entityId);

        result.Should().BeEmpty();
    }

    // ── GetAccountByIdAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountByIdAsync_WithValidId_ReturnsAccount()
    {
        var account = SeedAccount(_entityId, "Savings");

        var result = await _sut.GetAccountByIdAsync(account.Id, _entityId);

        result.Id.Should().Be(account.Id);
        result.Name.Should().Be("Savings");
    }

    [Fact]
    public async Task GetAccountByIdAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId, "Mine");

        var act = () => _sut.GetAccountByIdAsync(account.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetAccountByIdAsync_WithNonExistentId_ThrowsKeyNotFoundException()
    {
        var act = () => _sut.GetAccountByIdAsync(Guid.NewGuid(), _entityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CreateAccountAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccountAsync_WithValidRequest_CreatesAccount()
    {
        var result = await _sut.CreateAccountAsync(new Application.DTOs.CreateAccountRequest
        {
            Name            = "New Account",
            AccountType     = AccountType.ASSET,
            Currency        = "AUD",
            StartingBalance = 500m
        }, _entityId);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("New Account");
        result.StartingBalance.Should().Be(500m);
        _db.Accounts.Any(a => a.Name == "New Account" && a.EntityId == _entityId).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAccountAsync_CreatesOpeningBalanceTransaction()
    {
        var result = await _sut.CreateAccountAsync(new Application.DTOs.CreateAccountRequest
        {
            Name            = "With Opening Balance",
            AccountType     = AccountType.ASSET,
            Currency        = "AUD",
            StartingBalance = 1000m
        }, _entityId);

        _db.Transactions.Any(t =>
            t.AccountId == result.Id &&
            t.TxnType   == TransactionType.OpeningBalance &&
            t.Amount    == 1000m
        ).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAccountAsync_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => _sut.CreateAccountAsync(new Application.DTOs.CreateAccountRequest
        {
            Name        = "  ",
            AccountType = AccountType.ASSET,
            Currency    = "AUD"
        }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name is required*");
    }

    // ── UpdateAccountAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAccountAsync_WithValidRequest_UpdatesFields()
    {
        var account = SeedAccount(_entityId, "Old Name");

        var result = await _sut.UpdateAccountAsync(account.Id, new Application.DTOs.UpdateAccountRequest
        {
            Name     = "New Name",
            Currency = "AUD",
            IsActive = true
        }, _entityId);

        result.Name.Should().Be("New Name");
        _db.Accounts.Find(account.Id)!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateAccountAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId, "Mine");

        var act = () => _sut.UpdateAccountAsync(account.Id, new Application.DTOs.UpdateAccountRequest
        {
            Name     = "Attempt",
            Currency = "AUD"
        }, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateAccountAsync_WhenStartingBalanceChanges_ReplacesOpeningBalanceTxn()
    {
        var account = SeedAccount(_entityId, "Changing Balance");
        // Seed the original opening balance transaction
        _db.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            EntityId  = _entityId,
            TxnType   = TransactionType.OpeningBalance,
            Amount    = 100m,
            Currency  = "AUD",
            Direction = TransactionDirection.Inflow,
            TxnTime   = account.StartingBalanceDate,
            TenantId  = "default"
        });
        await _db.SaveChangesAsync();

        await _sut.UpdateAccountAsync(account.Id, new Application.DTOs.UpdateAccountRequest
        {
            Name            = account.Name,
            Currency        = "AUD",
            StartingBalance = 999m
        }, _entityId);

        // Old anchor should be soft-deleted, new one created.
        // IgnoreQueryFilters is required because EF Core InMemory applies the global
        // !IsDeleted query filter, hiding soft-deleted transactions from normal queries.
        var txns = _db.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.AccountId == account.Id && t.TxnType == TransactionType.OpeningBalance)
            .ToList();

        txns.Should().Contain(t => !t.IsDeleted && t.Amount == 999m);
        txns.Should().Contain(t => t.IsDeleted && t.Amount == 100m);
    }

    // ── DeleteAccountAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccountAsync_SoftDeletesAccountAndCascades()
    {
        var account = SeedAccount(_entityId, "To Delete");
        _db.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            EntityId  = _entityId,
            TxnType   = TransactionType.Expense,
            Amount    = 50m,
            Currency  = "AUD",
            TxnTime   = DateTime.UtcNow,
            TenantId  = "default"
        });
        await _db.SaveChangesAsync();

        await _sut.DeleteAccountAsync(account.Id, _entityId);

        _db.Accounts.Find(account.Id)!.IsDeleted.Should().BeTrue();
        _db.Transactions.Where(t => t.AccountId == account.Id).All(t => t.IsDeleted).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAccountAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId, "Mine");

        var act = () => _sut.DeleteAccountAsync(account.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetDeletionImpactAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetDeletionImpactAsync_ReturnsCorrectCounts()
    {
        var account = SeedAccount(_entityId, "Impact Account");
        _db.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            EntityId  = _entityId,
            TxnType   = TransactionType.Expense,
            Amount    = 10m,
            Currency  = "AUD",
            TxnTime   = DateTime.UtcNow,
            TenantId  = "default"
        });
        await _db.SaveChangesAsync();

        var impact = await _sut.GetDeletionImpactAsync(account.Id, _entityId);

        impact.AccountId.Should().Be(account.Id);
        impact.TransactionCount.Should().Be(1);
    }

    // ── GetAccountSummaryAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountSummaryAsync_WithNoAccounts_ReturnsZeroNetWorth()
    {
        var result = await _sut.GetAccountSummaryAsync(_entityId);

        result.NetWorth.Should().Be(0);
        result.TotalAssets.Should().Be(0);
        result.TotalLiabilities.Should().Be(0);
    }

    [Fact]
    public async Task GetAccountSummaryAsync_CalculatesNetWorthCorrectly()
    {
        var asset = SeedAccount(_entityId, "Asset Account", AccountType.ASSET);
        var liability = SeedAccount(_entityId, "Liability Account", AccountType.LIABILITY);
        asset.CurrentBalance = 5000m;
        liability.CurrentBalance = -2000m;
        await _db.SaveChangesAsync();

        var result = await _sut.GetAccountSummaryAsync(_entityId);

        result.TotalAssets.Should().Be(5000m);
        result.TotalLiabilities.Should().Be(2000m);
        result.NetWorth.Should().Be(3000m);
    }

    // ── RecalculateBalanceAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RecalculateBalanceAsync_SumsInflowsAndOutflowsCorrectly()
    {
        var account = SeedAccount(_entityId, "Recalc", AccountType.ASSET);
        account.StartingBalance = 1000m;
        account.StartingBalanceDate = DateTime.UtcNow.AddYears(-1);
        _db.Transactions.AddRange(
            new Transaction { AccountId = account.Id, EntityId = _entityId, TxnType = TransactionType.Income,  Amount = 500m, Direction = TransactionDirection.Inflow,  TxnTime = DateTime.UtcNow, Currency = "AUD", TenantId = "default" },
            new Transaction { AccountId = account.Id, EntityId = _entityId, TxnType = TransactionType.Expense, Amount = 200m, Direction = TransactionDirection.Outflow, TxnTime = DateTime.UtcNow, Currency = "AUD", TenantId = "default" }
        );
        await _db.SaveChangesAsync();

        await _sut.RecalculateBalanceAsync(account.Id, _entityId);

        _db.Accounts.Find(account.Id)!.CurrentBalance.Should().Be(1300m); // 1000 + 500 - 200
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Account SeedAccount(Guid entityId, string name, AccountType type = AccountType.ASSET, bool isDeleted = false)
    {
        var account = new Account
        {
            Id                  = Guid.NewGuid(),
            EntityId            = entityId,
            Name                = name,
            AccountType         = type,
            Currency            = "AUD",
            TenantId            = "default",
            IsDeleted           = isDeleted,
            StartingBalance     = 0m,
            StartingBalanceDate = DateTime.UtcNow.AddMonths(-1)
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        return account;
    }
}
