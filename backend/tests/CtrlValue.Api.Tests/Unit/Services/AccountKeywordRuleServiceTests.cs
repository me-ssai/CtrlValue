using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="AccountKeywordRuleService"/>.
/// </summary>
public class AccountKeywordRuleServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly AccountKeywordRuleService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;

    public AccountKeywordRuleServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new AccountKeywordRuleService(_db);
        _db.Entities.Add(new Domain.Entities.Entity { Id = _entityId, Name = "Test", TenantId = "default" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEntityRulesOnly()
    {
        var account = SeedAccount(_entityId);
        SeedRule(_entityId, account.Id, "SAVINGS TRANSFER");
        SeedRule(Guid.NewGuid(), account.Id, "OTHER ENTITY RULE");

        var result = await _sut.GetAllAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Keyword.Should().Be("SAVINGS TRANSFER");
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidRequest_NormalizesKeyword()
    {
        var account = SeedAccount(_entityId);

        var result = await _sut.CreateAsync(_entityId, new CreateAccountKeywordRuleRequest
        {
            AccountId = account.Id,
            Keyword   = "Transfer to Savings",
            MatchType = KeywordMatchType.Contains
        });

        result.Id.Should().NotBeEmpty();
        result.Keyword.Should().Be("Transfer to Savings");
        result.NormalizedKeyword.Should().Be("TRANSFER TO SAVINGS");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateKeyword_ThrowsInvalidOperationException()
    {
        var account = SeedAccount(_entityId);
        SeedRule(_entityId, account.Id, "MORTGAGE");

        var act = () => _sut.CreateAsync(_entityId, new CreateAccountKeywordRuleRequest
        {
            AccountId = account.Id,
            Keyword   = "MORTGAGE",
            MatchType = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyKeyword_ThrowsArgumentException()
    {
        var account = SeedAccount(_entityId);

        var act = () => _sut.CreateAsync(_entityId, new CreateAccountKeywordRuleRequest
        {
            AccountId = account.Id,
            Keyword   = "  ",
            MatchType = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Keyword is required*");
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesKeyword()
    {
        var account = SeedAccount(_entityId);
        var rule = SeedRule(_entityId, account.Id, "OLD KEYWORD");

        var result = await _sut.UpdateAsync(_entityId, rule.Id, new UpdateAccountKeywordRuleRequest
        {
            AccountId = account.Id,
            Keyword   = "NEW KEYWORD",
            MatchType = KeywordMatchType.Contains
        });

        result.Keyword.Should().Be("NEW KEYWORD");
        result.NormalizedKeyword.Should().Be("NEW KEYWORD");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentRuleId_ThrowsKeyNotFoundException()
    {
        var account = SeedAccount(_entityId);

        var act = () => _sut.UpdateAsync(_entityId, Guid.NewGuid(), new UpdateAccountKeywordRuleRequest
        {
            AccountId = account.Id,
            Keyword   = "ANYTHING",
            MatchType = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesRule()
    {
        var account = SeedAccount(_entityId);
        var rule = SeedRule(_entityId, account.Id, "LOAN REPAYMENT");

        await _sut.DeleteAsync(_entityId, rule.Id);

        _db.AccountKeywordRules.FirstOrDefault(r => r.Id == rule.Id).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync(_entityId, Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ── MatchAccountAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MatchAccountAsync_ContainsMatch_ReturnsMatchingAccountId()
    {
        var account = SeedAccount(_entityId);
        SeedRule(_entityId, account.Id, "SAVINGS", KeywordMatchType.Contains);

        var result = await _sut.MatchAccountAsync(_entityId, "Transfer to Savings Account");

        result.Should().Be(account.Id);
    }

    [Fact]
    public async Task MatchAccountAsync_ExactMatch_ReturnsOnlyForExactText()
    {
        var account = SeedAccount(_entityId);
        SeedRule(_entityId, account.Id, "TRANSFER", KeywordMatchType.Exact);

        var noMatch = await _sut.MatchAccountAsync(_entityId, "TRANSFER TO SAVINGS");
        var match   = await _sut.MatchAccountAsync(_entityId, "TRANSFER");

        noMatch.Should().BeNull("'TRANSFER TO SAVINGS' is not an exact match for 'TRANSFER'");
        match.Should().Be(account.Id);
    }

    [Fact]
    public async Task MatchAccountAsync_StartsWithMatch_MatchesPrefix()
    {
        var account = SeedAccount(_entityId);
        SeedRule(_entityId, account.Id, "MORTGAGE", KeywordMatchType.StartsWith);

        var match   = await _sut.MatchAccountAsync(_entityId, "MORTGAGE REPAYMENT REF 123");
        var noMatch = await _sut.MatchAccountAsync(_entityId, "PAYMENT FOR MORTGAGE");

        match.Should().Be(account.Id);
        noMatch.Should().BeNull();
    }

    [Fact]
    public async Task MatchAccountAsync_WithNoMatchingRules_ReturnsNull()
    {
        var result = await _sut.MatchAccountAsync(_entityId, "Unrecognised transaction");

        result.Should().BeNull();
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
            StartingBalanceDate = DateTime.UtcNow
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        return account;
    }

    private AccountKeywordRule SeedRule(
        Guid entityId,
        Guid accountId,
        string keyword,
        KeywordMatchType matchType = KeywordMatchType.Contains,
        bool caseSensitive = false)
    {
        var rule = new AccountKeywordRule
        {
            Id                = Guid.NewGuid(),
            EntityId          = entityId,
            AccountId         = accountId,
            Keyword           = keyword,
            NormalizedKeyword = keyword.ToUpperInvariant(),
            MatchType         = matchType,
            IsCaseSensitive   = caseSensitive,
            TenantId          = "default"
        };
        _db.AccountKeywordRules.Add(rule);
        _db.SaveChanges();
        return rule;
    }
}
