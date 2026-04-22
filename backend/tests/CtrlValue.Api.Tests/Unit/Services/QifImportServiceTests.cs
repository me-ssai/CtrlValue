using System.Text;
using FluentAssertions;
using Moq;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.Interfaces;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

public class QifImportServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly Mock<IAiCategorizationService> _aiCat = new();
    private readonly Mock<IAccountService> _accountService = new();
    private readonly Mock<ICategoryKeywordRuleService> _keywordRules = new();
    private readonly Mock<IAccountKeywordRuleService> _accountKeywordRules = new();
    private readonly QifImportService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;

    public QifImportServiceTests()
    {
        _db = InMemoryDbFactory.Create();
        _aiCat.Setup(s => s.CategorizeAsync(
                It.IsAny<IReadOnlyList<ImportedTransactionsFileStaging>>(),
                It.IsAny<IReadOnlyList<Category>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _keywordRules
            .Setup(s => s.MatchCategoryAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((Guid?)null);
        _accountKeywordRules
            .Setup(s => s.MatchAccountAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((Guid?)null);
        _sut = new QifImportService(_db, _aiCat.Object, _accountService.Object, _keywordRules.Object, _accountKeywordRules.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Counter-leg duplicate detection ───────────────────────────────────────

    [Fact]
    public async Task UploadAndStageAsync_WhenTransactionMatchesExistingCounterLeg_MarksAsAlreadyImported()
    {
        // Arrange: savings account has an IMPORT_COUNTER transaction for a $500 inflow on 2024-01-15
        var savingsAccount = SeedAccount(_entityId);
        SeedCounterLegTransaction(_entityId, savingsAccount.Id,
            new DateTime(2024, 1, 15), amount: 500m, direction: TransactionDirection.Inflow);

        // The savings statement contains that same transfer (different description, same date/amount/direction)
        var qifStream = MakeQifStream("2024-01-15", "500.00", "Transfer from Checking");

        // Act
        var result = await _sut.UploadAndStageAsync(
            _entityId, savingsAccount.Id, allowDuplicates: false, dateFormat: null, qifStream, "savings.qif");

        // Assert
        result.AlreadyImportedRows.Should().Be(1);
        result.ValidRows.Should().Be(0);
    }

    [Fact]
    public async Task UploadAndStageAsync_WhenCounterLegIsOnDifferentAccount_RowRemainsValid()
    {
        // Arrange: counter leg exists on checking, but we are importing savings
        var checkingAccount = SeedAccount(_entityId);
        var savingsAccount = SeedAccount(_entityId);
        SeedCounterLegTransaction(_entityId, checkingAccount.Id,
            new DateTime(2024, 1, 15), amount: 500m, direction: TransactionDirection.Outflow);

        var qifStream = MakeQifStream("2024-01-15", "500.00", "Transfer from Checking");

        // Act
        var result = await _sut.UploadAndStageAsync(
            _entityId, savingsAccount.Id, allowDuplicates: false, dateFormat: null, qifStream, "savings.qif");

        // Assert — the savings row is not a duplicate of checking's counter leg
        result.ValidRows.Should().Be(1);
        result.AlreadyImportedRows.Should().Be(0);
    }

    [Fact]
    public async Task UploadAndStageAsync_WhenCounterLegAmountDiffers_RowRemainsValid()
    {
        // Arrange: counter leg on savings is for $999, but the import row is $500
        var savingsAccount = SeedAccount(_entityId);
        SeedCounterLegTransaction(_entityId, savingsAccount.Id,
            new DateTime(2024, 1, 15), amount: 999m, direction: TransactionDirection.Inflow);

        var qifStream = MakeQifStream("2024-01-15", "500.00", "Transfer from Checking");

        // Act
        var result = await _sut.UploadAndStageAsync(
            _entityId, savingsAccount.Id, allowDuplicates: false, dateFormat: null, qifStream, "savings.qif");

        // Assert
        result.ValidRows.Should().Be(1);
        result.AlreadyImportedRows.Should().Be(0);
    }

    // ── Account keyword auto-population ──────────────────────────────────────

    [Fact]
    public async Task UploadAndStageAsync_WhenDescriptionMatchesAccountKeyword_SetsCounterAccountId()
    {
        // Arrange
        var checkingAccount = SeedAccount(_entityId);
        var savingsAccount  = SeedAccount(_entityId);

        _accountKeywordRules
            .Setup(s => s.MatchAccountAsync(_entityId, It.Is<string>(d => d.Contains("SAVINGS"))))
            .ReturnsAsync(savingsAccount.Id);

        var qifStream = MakeQifStream("2024-03-01", "-200.00", "TRANSFER TO SAVINGS");

        // Act
        var result = await _sut.UploadAndStageAsync(
            _entityId, checkingAccount.Id, allowDuplicates: false, dateFormat: null, qifStream, "checking.qif");

        // Assert
        var staged = _db.ImportedTransactionsFilesStaging
            .Single(r => r.ImportedTransactionsFileId == result.Id);
        staged.CounterAccountId.Should().Be(savingsAccount.Id);
    }

    [Fact]
    public async Task UploadAndStageAsync_WhenKeywordMatchesPrimaryAccount_DoesNotSetCounterAccountId()
    {
        // Arrange: keyword rule would match the same account being imported (self-referential)
        var checkingAccount = SeedAccount(_entityId);

        _accountKeywordRules
            .Setup(s => s.MatchAccountAsync(_entityId, It.IsAny<string>()))
            .ReturnsAsync(checkingAccount.Id); // returns same account as primary

        var qifStream = MakeQifStream("2024-03-01", "-200.00", "TRANSFER");

        // Act
        var result = await _sut.UploadAndStageAsync(
            _entityId, checkingAccount.Id, allowDuplicates: false, dateFormat: null, qifStream, "checking.qif");

        // Assert — self-referential match must be ignored
        var staged = _db.ImportedTransactionsFilesStaging
            .Single(r => r.ImportedTransactionsFileId == result.Id);
        staged.CounterAccountId.Should().BeNull();
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

    private void SeedCounterLegTransaction(
        Guid entityId, Guid accountId, DateTime date, decimal amount, TransactionDirection direction)
    {
        _db.Transactions.Add(new Transaction
        {
            Id              = Guid.NewGuid(),
            EntityId        = entityId,
            AccountId       = accountId,
            Description     = "Counter leg",
            TxnType         = TransactionType.Transfer,
            Direction       = direction,
            Amount          = amount,
            Currency        = "AUD",
            TxnTime         = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Source          = "IMPORT_COUNTER",
            TenantId        = "default",
            TransferGroupId = Guid.NewGuid()
        });
        _db.SaveChanges();
    }

    private static Stream MakeQifStream(string date, string amount, string description)
    {
        var content = $"!Type:Bank\nD{date}\nT{amount}\nP{description}\n^\n";
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
