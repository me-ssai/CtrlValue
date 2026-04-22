using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="CategoryKeywordRuleService"/>.
/// </summary>
public class CategoryKeywordRuleServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly CategoryKeywordRuleService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;

    public CategoryKeywordRuleServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new CategoryKeywordRuleService(_db);
        _db.Entities.Add(new Domain.Entities.Entity { Id = _entityId, Name = "Test", TenantId = "default" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEntityRulesOnly()
    {
        var cat = SeedCategory("Groceries");
        SeedRule(_entityId, cat.Id, "WOOLWORTHS");
        SeedRule(Guid.NewGuid(), cat.Id, "COLES"); // other entity

        var result = await _sut.GetAllAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Keyword.Should().Be("WOOLWORTHS");
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesRule()
    {
        var cat = SeedCategory("Shopping");

        var result = await _sut.CreateAsync(_entityId, new CreateCategoryKeywordRuleRequest
        {
            CategoryId = cat.Id,
            Keyword    = "Amazon",
            MatchType  = KeywordMatchType.Contains
        });

        result.Id.Should().NotBeEmpty();
        result.Keyword.Should().Be("Amazon");
        result.NormalizedKeyword.Should().Be("AMAZON");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateKeyword_ThrowsInvalidOperationException()
    {
        var cat = SeedCategory("Food");
        SeedRule(_entityId, cat.Id, "COLES");

        var act = () => _sut.CreateAsync(_entityId, new CreateCategoryKeywordRuleRequest
        {
            CategoryId = cat.Id,
            Keyword    = "COLES",
            MatchType  = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyKeyword_ThrowsArgumentException()
    {
        var cat = SeedCategory("Food");

        var act = () => _sut.CreateAsync(_entityId, new CreateCategoryKeywordRuleRequest
        {
            CategoryId = cat.Id,
            Keyword    = "  ",
            MatchType  = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Keyword is required*");
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesRule()
    {
        var cat = SeedCategory("Transport");
        var rule = SeedRule(_entityId, cat.Id, "UBER");

        var result = await _sut.UpdateAsync(_entityId, rule.Id, new UpdateCategoryKeywordRuleRequest
        {
            CategoryId = cat.Id,
            Keyword    = "LYFT",
            MatchType  = KeywordMatchType.Contains
        });

        result.Keyword.Should().Be("LYFT");
        result.NormalizedKeyword.Should().Be("LYFT");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentRuleId_ThrowsKeyNotFoundException()
    {
        var cat = SeedCategory("Food");

        var act = () => _sut.UpdateAsync(_entityId, Guid.NewGuid(), new UpdateCategoryKeywordRuleRequest
        {
            CategoryId = cat.Id,
            Keyword    = "COLES",
            MatchType  = KeywordMatchType.Contains
        });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesRule()
    {
        var cat = SeedCategory("Food");
        var rule = SeedRule(_entityId, cat.Id, "ALDI");

        await _sut.DeleteAsync(_entityId, rule.Id);

        // HandleSoftDeletes converts Remove→soft-delete; global query filter hides it.
        _db.CategoryKeywordRules.FirstOrDefault(r => r.Id == rule.Id).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var act = () => _sut.DeleteAsync(_entityId, Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    // ── MatchCategoryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task MatchCategoryAsync_ContainsMatch_ReturnsMatchingCategoryId()
    {
        var cat  = SeedCategory("Groceries");
        SeedRule(_entityId, cat.Id, "WOOLWORTHS", KeywordMatchType.Contains);

        var result = await _sut.MatchCategoryAsync(_entityId, "WOOLWORTHS MIRANDA 4521");

        result.Should().Be(cat.Id);
    }

    [Fact]
    public async Task MatchCategoryAsync_ExactMatch_ReturnsMatchOnly_ForExactText()
    {
        var cat = SeedCategory("Transport");
        SeedRule(_entityId, cat.Id, "UBER", KeywordMatchType.Exact);

        var noMatch = await _sut.MatchCategoryAsync(_entityId, "UBER EATS");
        var match   = await _sut.MatchCategoryAsync(_entityId, "UBER");

        noMatch.Should().BeNull("'UBER EATS' is not an exact match for 'UBER'");
        match.Should().Be(cat.Id);
    }

    [Fact]
    public async Task MatchCategoryAsync_StartsWithMatch_MatchesPrefix()
    {
        var cat = SeedCategory("Shopping");
        SeedRule(_entityId, cat.Id, "AMAZON", KeywordMatchType.StartsWith);

        var match   = await _sut.MatchCategoryAsync(_entityId, "AMAZON AU 123");
        var noMatch = await _sut.MatchCategoryAsync(_entityId, "PURCHASE AT AMAZON");

        match.Should().Be(cat.Id);
        noMatch.Should().BeNull();
    }

    [Fact]
    public async Task MatchCategoryAsync_RegexMatch_MatchesPattern()
    {
        var cat = SeedCategory("Utilities");
        SeedRule(_entityId, cat.Id, @"ORIGIN\s+ENERGY", KeywordMatchType.Regex);

        var match   = await _sut.MatchCategoryAsync(_entityId, "ORIGIN ENERGY BILL");
        var noMatch = await _sut.MatchCategoryAsync(_entityId, "ORIGIN");

        match.Should().Be(cat.Id);
        noMatch.Should().BeNull();
    }

    [Fact]
    public async Task MatchCategoryAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var cat = SeedCategory("Dining");
        SeedRule(_entityId, cat.Id, "MCDONALD", KeywordMatchType.Contains, caseSensitive: false);

        var result = await _sut.MatchCategoryAsync(_entityId, "McDonald's Parramatta");

        result.Should().Be(cat.Id);
    }

    [Fact]
    public async Task MatchCategoryAsync_WithNoMatchingRules_ReturnsNull()
    {
        var result = await _sut.MatchCategoryAsync(_entityId, "Unknown merchant");

        result.Should().BeNull();
    }

    // ── ApplyRulesToWorkspaceAsync ────────────────────────────────────────────

    [Fact(Skip = "ApplyRulesToWorkspaceAsync uses ExecuteUpdateAsync which is not supported by the EF Core InMemory provider.")]
    public async Task ApplyRulesToWorkspaceAsync_CategorizesMatchingUncategorizedTransactions()
    {
        var cat    = SeedCategory("Groceries");
        SeedRule(_entityId, cat.Id, "WOOLWORTHS", KeywordMatchType.Contains);
        var account = SeedAccount();
        SeedUncategorizedTransaction(account.Id, "WOOLWORTHS BONDI 1234");
        SeedUncategorizedTransaction(account.Id, "UNRELATED MERCHANT");

        var count = await _sut.ApplyRulesToWorkspaceAsync(_entityId);

        count.Should().Be(1);
        _db.Transactions.Where(t => t.EntityId == _entityId && !t.IsDeleted)
            .Count(t => t.CategoryId == cat.Id).Should().Be(1);
    }

    [Fact(Skip = "ApplyRulesToWorkspaceAsync uses ExecuteUpdateAsync which is not supported by the EF Core InMemory provider.")]
    public async Task ApplyRulesToWorkspaceAsync_DoesNotOverwriteAlreadyCategorized()
    {
        var cat      = SeedCategory("Groceries");
        var otherCat = SeedCategory("Other");
        SeedRule(_entityId, cat.Id, "WOOLWORTHS", KeywordMatchType.Contains);
        var account = SeedAccount();

        // Transaction already categorised as "Other"
        _db.Transactions.Add(new Transaction
        {
            Id          = Guid.NewGuid(),
            EntityId    = _entityId,
            AccountId   = account.Id,
            Description = "WOOLWORTHS BONDI",
            Amount      = 50m,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow,
            TenantId    = "default",
            CategoryId  = otherCat.Id  // already set
        });
        await _db.SaveChangesAsync();

        var count = await _sut.ApplyRulesToWorkspaceAsync(_entityId);

        count.Should().Be(0, "already-categorised transactions must not be overwritten");
    }

    [Fact]
    public async Task ApplyRulesToWorkspaceAsync_WithNoUncategorizedTransactions_Returns0()
    {
        var count = await _sut.ApplyRulesToWorkspaceAsync(_entityId);

        count.Should().Be(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Category SeedCategory(string name)
    {
        var cat = new Category
        {
            Id           = Guid.NewGuid(),
            EntityId     = _entityId,
            Name         = name,
            CategoryType = CategoryType.EXPENSE,
            TenantId     = "default"
        };
        _db.Categories.Add(cat);
        _db.SaveChanges();
        return cat;
    }

    private CategoryKeywordRule SeedRule(
        Guid entityId,
        Guid categoryId,
        string keyword,
        KeywordMatchType matchType = KeywordMatchType.Contains,
        bool caseSensitive = false)
    {
        var rule = new CategoryKeywordRule
        {
            Id                = Guid.NewGuid(),
            EntityId          = entityId,
            CategoryId        = categoryId,
            Keyword           = keyword,
            NormalizedKeyword = keyword.ToUpperInvariant(),
            MatchType         = matchType,
            IsCaseSensitive   = caseSensitive,
            TenantId          = "default"
        };
        _db.CategoryKeywordRules.Add(rule);
        _db.SaveChanges();
        return rule;
    }

    private Account SeedAccount()
    {
        var account = new Account
        {
            Id                  = Guid.NewGuid(),
            EntityId            = _entityId,
            Name                = "Test",
            AccountType         = AccountType.ASSET,
            Currency            = "AUD",
            TenantId            = "default",
            StartingBalanceDate = DateTime.UtcNow
        };
        _db.Accounts.Add(account);
        _db.SaveChanges();
        return account;
    }

    private void SeedUncategorizedTransaction(Guid accountId, string description)
    {
        _db.Transactions.Add(new Transaction
        {
            Id          = Guid.NewGuid(),
            EntityId    = _entityId,
            AccountId   = accountId,
            Description = description,
            Amount      = 10m,
            Currency    = "AUD",
            TxnTime     = DateTime.UtcNow,
            TenantId    = "default",
            CategoryId  = null  // uncategorized
        });
        _db.SaveChanges();
    }
}
