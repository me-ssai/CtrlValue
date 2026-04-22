using FluentAssertions;
using CtrlValue.Api.Tests.Infrastructure;
using CtrlValue.Application.Services;
using CtrlValue.Domain;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using Xunit;

namespace CtrlValue.Api.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="PermissionService"/>.
/// </summary>
public class PermissionServiceTests : IDisposable
{
    private readonly CtrlValue.Infrastructure.Data.AppDbContext _db;
    private readonly PermissionService _sut;
    private readonly Guid _entityId = Guid.NewGuid();
    private readonly Guid _otherEntityId = Guid.NewGuid();

    public PermissionServiceTests()
    {
        _db  = InMemoryDbFactory.Create();
        _sut = new PermissionService(_db);
        SeedRoleData();
    }

    public void Dispose() => _db.Dispose();

    // ── HasAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasAsync_OwnerRole_HasAllPermissions()
    {
        var userId = SeedUserWithRole(UserRole.User, _entityId, Permissions.All.ToArray());

        foreach (var permission in Permissions.All)
        {
            var result = await _sut.HasAsync(userId, _entityId, permission);
            result.Should().BeTrue($"owner should have '{permission}'");
        }
    }

    [Fact]
    public async Task HasAsync_ViewerRole_HasReadPermissions()
    {
        var userId = SeedUserWithRole(UserRole.User, _entityId,
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read,
            Permissions.Transactions.Read);

        (await _sut.HasAsync(userId, _entityId, Permissions.Accounts.Read)).Should().BeTrue();
        (await _sut.HasAsync(userId, _entityId, Permissions.Transactions.Read)).Should().BeTrue();
    }

    [Fact]
    public async Task HasAsync_ViewerRole_DoesNotHaveWritePermissions()
    {
        var userId = SeedUserWithRole(UserRole.User, _entityId,
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read);

        (await _sut.HasAsync(userId, _entityId, Permissions.Accounts.Write)).Should().BeFalse();
        (await _sut.HasAsync(userId, _entityId, Permissions.Transactions.Write)).Should().BeFalse();
    }

    [Fact]
    public async Task HasAsync_SuperAdmin_ReturnsTrueRegardlessOfEntityRole()
    {
        var superAdminId = SeedUser(UserRole.SuperAdmin);
        // SuperAdmin has NO EntityUser record for this entity

        (await _sut.HasAsync(superAdminId, _entityId, Permissions.Entity.Manage)).Should().BeTrue();
    }

    [Fact]
    public async Task HasAsync_SiteAdmin_ReturnsTrueRegardlessOfEntityRole()
    {
        var siteAdminId = SeedUser(UserRole.SiteAdmin);

        (await _sut.HasAsync(siteAdminId, _entityId, Permissions.Accounts.Write)).Should().BeTrue();
    }

    [Fact]
    public async Task HasAsync_UserNotInEntity_ReturnsFalse()
    {
        var outsiderId = SeedUser(UserRole.User);
        // No EntityUser record for this user

        (await _sut.HasAsync(outsiderId, _entityId, Permissions.Accounts.Read)).Should().BeFalse();
    }

    [Fact]
    public async Task HasAsync_UserInDifferentEntity_ReturnsFalse()
    {
        var userId = SeedUserWithRole(UserRole.User, _otherEntityId, Permissions.Accounts.Read);

        // User has permissions in _otherEntityId, not _entityId
        (await _sut.HasAsync(userId, _entityId, Permissions.Accounts.Read)).Should().BeFalse();
    }

    [Fact]
    public async Task HasAsync_DeletedEntityUser_ReturnsFalse()
    {
        var roleId = SeedRole(_entityId, Permissions.Accounts.Read);
        var userId = SeedUser(UserRole.User);
        _db.EntityUsers.Add(new EntityUser
        {
            Id           = Guid.NewGuid(),
            EntityId     = _entityId,
            UserId       = userId,
            CustomRoleId = roleId,
            TenantId     = "default",
            IsDeleted    = true  // soft-deleted membership
        });
        _db.SaveChanges();

        (await _sut.HasAsync(userId, _entityId, Permissions.Accounts.Read)).Should().BeFalse();
    }

    // ── RequireAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RequireAsync_WhenPermissionGranted_DoesNotThrow()
    {
        var userId = SeedUserWithRole(UserRole.User, _entityId, Permissions.Accounts.Read);

        var act = () => _sut.RequireAsync(userId, _entityId, Permissions.Accounts.Read);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RequireAsync_WhenPermissionDenied_ThrowsUnauthorizedAccessException()
    {
        var userId = SeedUserWithRole(UserRole.User, _entityId, Permissions.Accounts.Read);

        var act = () => _sut.RequireAsync(userId, _entityId, Permissions.Accounts.Write);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*'{Permissions.Accounts.Write}'*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SeedRoleData()
    {
        // Ensure entities exist so FK constraints are satisfied
        _db.Entities.Add(new Domain.Entities.Entity { Id = _entityId, Name = "Test", TenantId = "default" });
        _db.Entities.Add(new Domain.Entities.Entity { Id = _otherEntityId, Name = "Other", TenantId = "default" });
        _db.SaveChanges();
    }

    private Guid SeedUser(UserRole role)
    {
        var user = new User
        {
            Id               = Guid.NewGuid(),
            Email            = $"user-{Guid.NewGuid()}@test.com",
            PasswordHash     = "hash",
            TenantId         = "default",
            IsEmailConfirmed = true,
            Role             = role
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }

    private Guid SeedRole(Guid entityId, params string[] permissions)
    {
        var role = new EntityCustomRole
        {
            Id       = Guid.NewGuid(),
            EntityId = entityId,
            Name     = $"Role_{Guid.NewGuid()}",
            IsSystem = false,
            TenantId = "default"
        };
        _db.EntityCustomRoles.Add(role);
        foreach (var perm in permissions)
        {
            _db.EntityRolePermissions.Add(new EntityRolePermission
            {
                RoleId        = role.Id,
                PermissionKey = perm
            });
        }
        _db.SaveChanges();
        return role.Id;
    }

    private Guid SeedUserWithRole(UserRole userRole, Guid entityId, params string[] permissions)
    {
        var userId = SeedUser(userRole);
        var roleId = SeedRole(entityId, permissions);
        _db.EntityUsers.Add(new EntityUser
        {
            Id           = Guid.NewGuid(),
            EntityId     = entityId,
            UserId       = userId,
            CustomRoleId = roleId,
            TenantId     = "default"
        });
        _db.SaveChanges();
        return userId;
    }
}
