namespace CtrlValue.Domain;

/// <summary>
/// Canonical permission keys used throughout the application.
/// These string constants are the single source of truth — service checks, DB rows,
/// and frontend role management UI all reference these keys.
/// </summary>
public static class Permissions
{
    public static class Dashboard
    {
        public const string Read = "dashboard:read";
    }

    public static class Accounts
    {
        public const string Read  = "accounts:read";
        public const string Write = "accounts:write";
    }

    public static class Transactions
    {
        public const string Read  = "transactions:read";
        public const string Write = "transactions:write";
    }

    public static class Investments
    {
        public const string Read  = "investments:read";
        public const string Write = "investments:write";
    }

    public static class Budgets
    {
        public const string Read  = "budgets:read";
        public const string Write = "budgets:write";
    }

    public static class Reports
    {
        public const string Read = "reports:read";
    }

    public static class Members
    {
        public const string Manage = "members:manage";
    }

    public static class Entity
    {
        public const string Manage = "entity:manage";
    }

    public static class Agent
    {
        public const string Read       = "agent:read";
        public const string Chat       = "agent:chat";
        public const string AdminFlags = "agent:admin:flags";
    }

    /// <summary>All permission keys as a flat collection — useful for seeding and validation.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Dashboard.Read,
        Accounts.Read,  Accounts.Write,
        Transactions.Read, Transactions.Write,
        Investments.Read,  Investments.Write,
        Budgets.Read,      Budgets.Write,
        Reports.Read,
        Members.Manage,
        Entity.Manage,
        Agent.Read, Agent.Chat, Agent.AdminFlags
    ];
}
