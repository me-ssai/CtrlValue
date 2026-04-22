namespace CtrlValue.Api.Tests.Infrastructure;

/// <summary>
/// Stable GUIDs used consistently across test helpers and seed data.
/// Using fixed GUIDs avoids ordering issues and makes assertions deterministic.
/// </summary>
public static class WellKnownIds
{
    // Users
    public static readonly Guid OwnerUserId     = new("a0000001-0000-0000-0000-000000000001");
    public static readonly Guid EditorUserId    = new("a0000001-0000-0000-0000-000000000002");
    public static readonly Guid ViewerUserId    = new("a0000001-0000-0000-0000-000000000003");
    public static readonly Guid SiteAdminUserId = new("a0000001-0000-0000-0000-000000000004");
    public static readonly Guid SuperAdminUserId= new("a0000001-0000-0000-0000-000000000005");
    public static readonly Guid OtherUserId     = new("a0000001-0000-0000-0000-000000000099");

    // Entities
    public static readonly Guid EntityId        = new("b0000001-0000-0000-0000-000000000001");
    public static readonly Guid OtherEntityId   = new("b0000001-0000-0000-0000-000000000002");

    // Roles
    public static readonly Guid OwnerRoleId     = new("c0000001-0000-0000-0000-000000000001");
    public static readonly Guid EditorRoleId    = new("c0000001-0000-0000-0000-000000000002");
    public static readonly Guid ViewerRoleId    = new("c0000001-0000-0000-0000-000000000003");

    // Accounts
    public static readonly Guid AccountId       = new("d0000001-0000-0000-0000-000000000001");
    public static readonly Guid Account2Id      = new("d0000001-0000-0000-0000-000000000002");
    public static readonly Guid OtherAccountId  = new("d0000001-0000-0000-0000-000000000099");

    // Transactions
    public static readonly Guid TransactionId   = new("e0000001-0000-0000-0000-000000000001");
    public static readonly Guid Transaction2Id  = new("e0000001-0000-0000-0000-000000000002");

    // Categories
    public static readonly Guid CategoryId      = new("f0000001-0000-0000-0000-000000000001");
    public static readonly Guid Category2Id     = new("f0000001-0000-0000-0000-000000000002");
}
