using FluentAssertions;
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
/// Unit tests for <see cref="TransactionService"/>.
/// </summary>
public class TransactionServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly Mock<IAccountService> _accountServiceMock = new();
    private readonly TransactionService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;
    private readonly Guid _otherEntityId = WellKnownIds.OtherEntityId;

    public TransactionServiceTests()
    {
        _db = InMemoryDbFactory.Create();
        _accountServiceMock.Setup(s => s.RecalculateBalanceAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.CompletedTask);
        _sut = new TransactionService(_db, _accountServiceMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── GetTransactionsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsAsync_ReturnsOnlyEntityTransactions()
    {
        var account = SeedAccount(_entityId);
        SeedTransaction(_entityId, account.Id, "Mine");
        SeedTransaction(_otherEntityId, account.Id, "Other Entity");

        var result = await _sut.GetTransactionsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Mine");
    }

    [Fact]
    public async Task GetTransactionsAsync_ExcludesSoftDeletedTransactions()
    {
        var account = SeedAccount(_entityId);
        SeedTransaction(_entityId, account.Id, "Active");
        SeedTransaction(_entityId, account.Id, "Deleted", isDeleted: true);

        var result = await _sut.GetTransactionsAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Active");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithDateRange_FiltersCorrectly()
    {
        var account = SeedAccount(_entityId);
        SeedTransactionAt(_entityId, account.Id, "Old", DateTime.UtcNow.AddMonths(-6));
        SeedTransactionAt(_entityId, account.Id, "Recent", DateTime.UtcNow.AddDays(-5));

        var start = DateTime.UtcNow.AddMonths(-1);
        var end   = DateTime.UtcNow;

        var result = await _sut.GetTransactionsAsync(_entityId, start, end);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Recent");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithNoDateRange_ReturnsAllNonDeleted()
    {
        var account = SeedAccount(_entityId);
        SeedTransactionAt(_entityId, account.Id, "Old", DateTime.UtcNow.AddYears(-2));
        SeedTransactionAt(_entityId, account.Id, "New", DateTime.UtcNow);

        var result = await _sut.GetTransactionsAsync(_entityId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTransactionsAsync_OrdersNewestFirst()
    {
        var account = SeedAccount(_entityId);
        SeedTransactionAt(_entityId, account.Id, "First", DateTime.UtcNow.AddDays(-10));
        SeedTransactionAt(_entityId, account.Id, "Second", DateTime.UtcNow.AddDays(-5));

        var result = await _sut.GetTransactionsAsync(_entityId);

        result[0].Description.Should().Be("Second");
        result[1].Description.Should().Be("First");
    }

    // ── GetTransactionsByAccountAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsByAccountAsync_ReturnsOnlyAccountTransactions()
    {
        var account1 = SeedAccount(_entityId);
        var account2 = SeedAccount(_entityId);
        SeedTransaction(_entityId, account1.Id, "Account1 Txn");
        SeedTransaction(_entityId, account2.Id, "Account2 Txn");

        var result = await _sut.GetTransactionsByAccountAsync(account1.Id, _entityId);

        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Account1 Txn");
    }

    [Fact]
    public async Task GetTransactionsByAccountAsync_WithOtherEntityAccount_ReturnsEmpty()
    {
        var account = SeedAccount(_entityId);
        SeedTransaction(_entityId, account.Id, "Mine");

        // Query with other entity ID — no cross-entity access
        var result = await _sut.GetTransactionsByAccountAsync(account.Id, _otherEntityId);

        result.Should().BeEmpty();
    }

    // ── GetTransactionByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionByIdAsync_WithValidId_ReturnsDto()
    {
        var account = SeedAccount(_entityId);
        var txn = SeedTransaction(_entityId, account.Id, "Lookup me");

        var result = await _sut.GetTransactionByIdAsync(txn.Id, _entityId);

        result.Id.Should().Be(txn.Id);
        result.Description.Should().Be("Lookup me");
    }

    [Fact]
    public async Task GetTransactionByIdAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId);
        var txn = SeedTransaction(_entityId, account.Id, "Mine");

        var act = () => _sut.GetTransactionByIdAsync(txn.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetTransactionByIdAsync_WithNonExistentId_ThrowsKeyNotFoundException()
    {
        var act = () => _sut.GetTransactionByIdAsync(Guid.NewGuid(), _entityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CreateTransactionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateTransactionAsync_WithValidRequest_CreatesAndReturnsDto()
    {
        var account = SeedAccount(_entityId);

        var result = await _sut.CreateTransactionAsync(new CreateTransactionRequest
        {
            AccountId   = account.Id,
            Amount      = 100m,
            Description = "Coffee",
            TxnType     = TransactionType.Expense,
            Direction   = TransactionDirection.Outflow,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow
        }, _entityId);

        result.Id.Should().NotBeEmpty();
        result.Description.Should().Be("Coffee");
        result.Amount.Should().Be(100m);
        _db.Transactions.Any(t => t.Id == result.Id && !t.IsDeleted).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTransactionAsync_TriggersBalanceRecalculation()
    {
        var account = SeedAccount(_entityId);

        await _sut.CreateTransactionAsync(new CreateTransactionRequest
        {
            AccountId   = account.Id,
            Amount      = 50m,
            Description = "Recalc test",
            TxnType     = TransactionType.Expense,
            Direction   = TransactionDirection.Outflow,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow
        }, _entityId);

        _accountServiceMock.Verify(s => s.RecalculateBalanceAsync(account.Id, _entityId), Times.Once);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithNegativeAmount_ThrowsArgumentException()
    {
        var account = SeedAccount(_entityId);

        var act = () => _sut.CreateTransactionAsync(new CreateTransactionRequest
        {
            AccountId   = account.Id,
            Amount      = -10m,
            Description = "Negative",
            TxnType     = TransactionType.Expense,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow
        }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task CreateTransactionAsync_WithEmptyDescription_ThrowsArgumentException()
    {
        var account = SeedAccount(_entityId);

        var act = () => _sut.CreateTransactionAsync(new CreateTransactionRequest
        {
            AccountId   = account.Id,
            Amount      = 10m,
            Description = "",
            TxnType     = TransactionType.Expense,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow
        }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*description is required*");
    }

    [Fact]
    public async Task CreateTransactionAsync_WithNonExistentAccount_ThrowsArgumentException()
    {
        var act = () => _sut.CreateTransactionAsync(new CreateTransactionRequest
        {
            AccountId   = Guid.NewGuid(),
            Amount      = 50m,
            Description = "No account",
            TxnType     = TransactionType.Expense,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow
        }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Account not found*");
    }

    // ── DeleteTransactionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTransactionAsync_SoftDeletesTransaction()
    {
        var account = SeedAccount(_entityId);
        var txn = SeedTransaction(_entityId, account.Id, "To delete");

        await _sut.DeleteTransactionAsync(txn.Id, _entityId);

        _db.Transactions.Find(txn.Id)!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTransactionAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId);
        var txn = SeedTransaction(_entityId, account.Id, "Mine");

        var act = () => _sut.DeleteTransactionAsync(txn.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── BulkDeleteTransactionsAsync ───────────────────────────────────────────

    [Fact(Skip = "BulkDeleteTransactionsAsync uses BeginTransactionAsync which is not supported by the EF Core InMemory provider. Covered by integration tests.")]
    public async Task BulkDeleteTransactionsAsync_DeletesAllListedTransactions()
    {
        var account = SeedAccount(_entityId);
        var t1 = SeedTransaction(_entityId, account.Id, "T1");
        var t2 = SeedTransaction(_entityId, account.Id, "T2");

        await _sut.BulkDeleteTransactionsAsync(new List<Guid> { t1.Id, t2.Id }, _entityId);

        _db.Transactions.Find(t1.Id)!.IsDeleted.Should().BeTrue();
        _db.Transactions.Find(t2.Id)!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task BulkDeleteTransactionsAsync_WithEmptyList_ThrowsArgumentException()
    {
        var act = () => _sut.BulkDeleteTransactionsAsync(new List<Guid>(), _entityId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact(Skip = "BulkDeleteTransactionsAsync uses BeginTransactionAsync which is not supported by the EF Core InMemory provider. Covered by integration tests.")]
    public async Task BulkDeleteTransactionsAsync_WithCrossEntityIds_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId);
        var mine = SeedTransaction(_entityId, account.Id, "Mine");
        var other = SeedTransaction(_otherEntityId, account.Id, "Other");

        var act = () => _sut.BulkDeleteTransactionsAsync(new List<Guid> { mine.Id, other.Id }, _entityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Account SeedAccount(Guid entityId)
    {
        var account = new Account
        {
            Id                  = Guid.NewGuid(),
            EntityId            = entityId,
            Name                = "Test Account",
            AccountType         = AccountType.ASSET,
            Currency            = "AUD",
            TenantId            = "default",
            StartingBalance     = 0m,
            StartingBalanceDate = DateTime.UtcNow.AddYears(-1)
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        return account;
    }

    private Transaction SeedTransaction(Guid entityId, Guid accountId, string description, bool isDeleted = false)
        => SeedTransactionAt(entityId, accountId, description, DateTime.UtcNow, isDeleted);

    private Transaction SeedTransactionAt(Guid entityId, Guid accountId, string description, DateTime txnTime, bool isDeleted = false)
    {
        var txn = new Transaction
        {
            Id          = Guid.NewGuid(),
            EntityId    = entityId,
            AccountId   = accountId,
            Description = description,
            TxnType     = TransactionType.Expense,
            Direction   = TransactionDirection.Outflow,
            Amount      = 10m,
            Currency    = "AUD",
            TxnTime     = txnTime,
            IsDeleted   = isDeleted,
            TenantId    = "default"
        };
        _db.Transactions.Add(txn);
        _db.SaveChanges();
        return txn;
    }
}
