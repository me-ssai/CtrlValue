using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.DTOs;
using CtrlValue.Application.Services;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="CategoryService"/>.
/// </summary>
public class CategoryServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly CategoryService _sut;
    private readonly Guid _entityId = WellKnownIds.EntityId;
    private readonly Guid _otherEntityId = WellKnownIds.OtherEntityId;

    public CategoryServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new CategoryService(_db);
        // Seed entities so FK constraints pass
        _db.Entities.Add(new Domain.Entities.Entity { Id = _entityId, Name = "Test", TenantId = "default" });
        _db.Entities.Add(new Domain.Entities.Entity { Id = _otherEntityId, Name = "Other", TenantId = "default" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetCategoriesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategoriesAsync_ReturnsEntityCategoriesOnly()
    {
        SeedCategory(_entityId, "Groceries");
        SeedCategory(_otherEntityId, "Other Entity Cat");

        var result = await _sut.GetCategoriesAsync(_entityId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Groceries");
    }

    [Fact]
    public async Task GetCategoriesAsync_FilteredByType_ReturnsOnlyMatchingCategories()
    {
        SeedCategory(_entityId, "Salary", CategoryType.INCOME);
        SeedCategory(_entityId, "Rent", CategoryType.EXPENSE);

        var result = await _sut.GetCategoriesAsync(_entityId, CategoryType.INCOME);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Salary");
    }

    [Fact]
    public async Task GetCategoriesAsync_WithNoCategories_ReturnsEmptyList()
    {
        var result = await _sut.GetCategoriesAsync(_entityId);

        result.Should().BeEmpty();
    }

    // ── GetCategoryByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCategoryByIdAsync_WithValidId_ReturnsCategory()
    {
        var cat = SeedCategory(_entityId, "Shopping");

        var result = await _sut.GetCategoryByIdAsync(cat.Id, _entityId);

        result.Id.Should().Be(cat.Id);
        result.Name.Should().Be("Shopping");
    }

    [Fact]
    public async Task GetCategoryByIdAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var cat = SeedCategory(_entityId, "Mine");

        var act = () => _sut.GetCategoryByIdAsync(cat.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CreateCategoryAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategoryAsync_WithValidRequest_CreatesCategory()
    {
        var result = await _sut.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name         = "Entertainment",
            CategoryType = CategoryType.EXPENSE
        }, _entityId);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Entertainment");
        result.CategoryType.Should().Be("EXPENSE");
        _db.Categories.Any(c => c.Name == "Entertainment" && c.EntityId == _entityId).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCategoryAsync_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => _sut.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name         = "  ",
            CategoryType = CategoryType.EXPENSE
        }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name is required*");
    }

    [Fact]
    public async Task CreateCategoryAsync_WithParentCategory_SetsParentRelationship()
    {
        var parent = SeedCategory(_entityId, "Income");

        var child = await _sut.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name             = "Freelance",
            CategoryType     = CategoryType.INCOME,
            ParentCategoryId = parent.Id
        }, _entityId);

        child.ParentCategoryId.Should().Be(parent.Id);
    }

    // ── UpdateCategoryAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCategoryAsync_WithValidRequest_UpdatesFields()
    {
        var cat = SeedCategory(_entityId, "Old Name");

        var result = await _sut.UpdateCategoryAsync(cat.Id, new UpdateCategoryRequest
        {
            Name     = "New Name",
            IsActive = true
        }, _entityId);

        result.Name.Should().Be("New Name");
        _db.Categories.Find(cat.Id)!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateCategoryAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var cat = SeedCategory(_entityId, "Mine");

        var act = () => _sut.UpdateCategoryAsync(cat.Id, new UpdateCategoryRequest { Name = "Attempted" }, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateCategoryAsync_WithEmptyName_ThrowsArgumentException()
    {
        var cat = SeedCategory(_entityId, "Valid");

        var act = () => _sut.UpdateCategoryAsync(cat.Id, new UpdateCategoryRequest { Name = "" }, _entityId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name is required*");
    }

    // ── DeleteCategoryAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCategoryAsync_WithValidId_RemovesCategory()
    {
        var cat = SeedCategory(_entityId, "To Delete");

        await _sut.DeleteCategoryAsync(cat.Id, _entityId);

        // HandleSoftDeletes converts Remove→soft-delete; global query filter hides it from normal queries.
        _db.Categories.FirstOrDefault(c => c.Id == cat.Id).Should().BeNull();
    }

    [Fact]
    public async Task DeleteCategoryAsync_WithWrongEntityId_ThrowsKeyNotFoundException()
    {
        var cat = SeedCategory(_entityId, "Mine");

        var act = () => _sut.DeleteCategoryAsync(cat.Id, _otherEntityId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Category SeedCategory(Guid entityId, string name, CategoryType type = CategoryType.EXPENSE)
    {
        var cat = new Category
        {
            Id           = Guid.NewGuid(),
            EntityId     = entityId,
            Name         = name,
            CategoryType = type,
            TenantId     = "default"
        };
        _db.Categories.Add(cat);
        _db.SaveChanges();
        return cat;
    }
}
