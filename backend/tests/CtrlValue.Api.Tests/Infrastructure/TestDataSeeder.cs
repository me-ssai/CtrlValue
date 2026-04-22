using CtrlValue.Domain;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;
using CtrlValue.Infrastructure.Data;

namespace CtrlValue.Api.Tests.Infrastructure;

/// <summary>
/// Seeds a deterministic, minimal dataset used by integration tests.
/// Uses <see cref="WellKnownIds"/> so tests can reference seeded records by stable GUIDs.
/// </summary>
public static class TestDataSeeder
{
    public static void Seed(AppDbContext db)
    {
        SeedUsers(db);
        SeedEntities(db);
        SeedRolesAndPermissions(db);
        SeedEntityUsers(db);
        SeedAccounts(db);
        SeedCategories(db);
        db.SaveChanges();
    }

    private static void SeedUsers(AppDbContext db)
    {
        db.Users.AddRange(
            new User
            {
                Id              = WellKnownIds.OwnerUserId,
                Email           = "owner@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Owner",
                LastName        = "User",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.User
            },
            new User
            {
                Id              = WellKnownIds.EditorUserId,
                Email           = "editor@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Editor",
                LastName        = "User",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.User
            },
            new User
            {
                Id              = WellKnownIds.ViewerUserId,
                Email           = "viewer@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Viewer",
                LastName        = "User",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.User
            },
            new User
            {
                Id              = WellKnownIds.SiteAdminUserId,
                Email           = "admin@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Site",
                LastName        = "Admin",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.SiteAdmin
            },
            new User
            {
                Id              = WellKnownIds.SuperAdminUserId,
                Email           = "super@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Super",
                LastName        = "Admin",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.SuperAdmin
            },
            new User
            {
                Id              = WellKnownIds.OtherUserId,
                Email           = "other@test.com",
                PasswordHash    = BCrypt.Net.BCrypt.HashPassword("Password1!"),
                FirstName       = "Other",
                LastName        = "User",
                TenantId        = "default",
                IsEmailConfirmed= true,
                Role            = UserRole.User
            }
        );
    }

    private static void SeedEntities(AppDbContext db)
    {
        db.Entities.AddRange(
            new Entity
            {
                Id           = WellKnownIds.EntityId,
                Name         = "Test Workspace",
                BaseCurrency = "AUD",
                TenantId     = "default"
            },
            new Entity
            {
                Id           = WellKnownIds.OtherEntityId,
                Name         = "Other Workspace",
                BaseCurrency = "AUD",
                TenantId     = "default"
            }
        );
    }

    private static void SeedRolesAndPermissions(AppDbContext db)
    {
        // Owner role — all permissions
        var ownerRole = new EntityCustomRole
        {
            Id       = WellKnownIds.OwnerRoleId,
            EntityId = WellKnownIds.EntityId,
            Name     = "Owner",
            IsSystem = true,
            TenantId = "default"
        };
        db.EntityCustomRoles.Add(ownerRole);

        foreach (var perm in Permissions.All)
        {
            db.EntityRolePermissions.Add(new EntityRolePermission
            {
                RoleId        = WellKnownIds.OwnerRoleId,
                PermissionKey = perm
            });
        }

        // Editor role — read+write on core, no entity:manage or members:manage
        var editorRole = new EntityCustomRole
        {
            Id       = WellKnownIds.EditorRoleId,
            EntityId = WellKnownIds.EntityId,
            Name     = "Editor",
            IsSystem = true,
            TenantId = "default"
        };
        db.EntityCustomRoles.Add(editorRole);

        var editorPerms = new[]
        {
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read, Permissions.Accounts.Write,
            Permissions.Transactions.Read, Permissions.Transactions.Write,
            Permissions.Investments.Read, Permissions.Investments.Write,
            Permissions.Budgets.Read, Permissions.Budgets.Write,
            Permissions.Reports.Read,
            Permissions.Agent.Read, Permissions.Agent.Chat
        };
        foreach (var perm in editorPerms)
        {
            db.EntityRolePermissions.Add(new EntityRolePermission
            {
                RoleId        = WellKnownIds.EditorRoleId,
                PermissionKey = perm
            });
        }

        // Viewer role — read only
        var viewerRole = new EntityCustomRole
        {
            Id       = WellKnownIds.ViewerRoleId,
            EntityId = WellKnownIds.EntityId,
            Name     = "Viewer",
            IsSystem = true,
            TenantId = "default"
        };
        db.EntityCustomRoles.Add(viewerRole);

        var viewerPerms = new[]
        {
            Permissions.Dashboard.Read,
            Permissions.Accounts.Read,
            Permissions.Transactions.Read,
            Permissions.Investments.Read,
            Permissions.Budgets.Read,
            Permissions.Reports.Read
        };
        foreach (var perm in viewerPerms)
        {
            db.EntityRolePermissions.Add(new EntityRolePermission
            {
                RoleId        = WellKnownIds.ViewerRoleId,
                PermissionKey = perm
            });
        }
    }

    private static void SeedEntityUsers(AppDbContext db)
    {
        db.EntityUsers.AddRange(
            new EntityUser
            {
                Id           = Guid.NewGuid(),
                EntityId     = WellKnownIds.EntityId,
                UserId       = WellKnownIds.OwnerUserId,
                CustomRoleId = WellKnownIds.OwnerRoleId,
                TenantId     = "default"
            },
            new EntityUser
            {
                Id           = Guid.NewGuid(),
                EntityId     = WellKnownIds.EntityId,
                UserId       = WellKnownIds.EditorUserId,
                CustomRoleId = WellKnownIds.EditorRoleId,
                TenantId     = "default"
            },
            new EntityUser
            {
                Id           = Guid.NewGuid(),
                EntityId     = WellKnownIds.EntityId,
                UserId       = WellKnownIds.ViewerUserId,
                CustomRoleId = WellKnownIds.ViewerRoleId,
                TenantId     = "default"
            }
        );
    }

    private static void SeedAccounts(AppDbContext db)
    {
        db.Accounts.AddRange(
            new Account
            {
                Id                  = WellKnownIds.AccountId,
                EntityId            = WellKnownIds.EntityId,
                Name                = "Everyday Checking",
                AccountType         = AccountType.ASSET,
                Currency            = "AUD",
                Institution         = "Test Bank",
                StartingBalance     = 1000m,
                StartingBalanceDate = DateTime.UtcNow.AddMonths(-6),
                TenantId            = "default"
            },
            new Account
            {
                Id                  = WellKnownIds.Account2Id,
                EntityId            = WellKnownIds.EntityId,
                Name                = "Credit Card",
                AccountType         = AccountType.LIABILITY,
                Currency            = "AUD",
                Institution         = "Test Bank",
                StartingBalance     = -500m,
                StartingBalanceDate = DateTime.UtcNow.AddMonths(-3),
                TenantId            = "default"
            },
            // Account belonging to a different entity — must not be returned for EntityId queries
            new Account
            {
                Id                  = WellKnownIds.OtherAccountId,
                EntityId            = WellKnownIds.OtherEntityId,
                Name                = "Other Entity Account",
                AccountType         = AccountType.ASSET,
                Currency            = "AUD",
                StartingBalance     = 0m,
                StartingBalanceDate = DateTime.UtcNow,
                TenantId            = "default"
            }
        );
    }

    private static void SeedCategories(AppDbContext db)
    {
        db.Categories.AddRange(
            new Category
            {
                Id           = WellKnownIds.CategoryId,
                EntityId     = WellKnownIds.EntityId,
                Name         = "Groceries",
                CategoryType = CategoryType.EXPENSE,
                TenantId     = "default"
            },
            new Category
            {
                Id           = WellKnownIds.Category2Id,
                EntityId     = WellKnownIds.EntityId,
                Name         = "Salary",
                CategoryType = CategoryType.INCOME,
                TenantId     = "default"
            }
        );
    }
}
