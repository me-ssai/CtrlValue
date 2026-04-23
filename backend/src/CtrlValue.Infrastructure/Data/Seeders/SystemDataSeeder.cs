using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CtrlValue.Domain.Entities;
using CtrlValue.Domain.Enums;

namespace CtrlValue.Infrastructure.Data.Seeders;

/// <summary>
/// Idempotent startup seeder for system-wide data that must exist before the app is usable.
/// Replaces seed logic that was previously embedded in EF migrations.
/// Runs on every startup; NOT EXISTS / conflict guards make re-runs safe.
/// </summary>
public class SystemDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SystemDataSeeder> _logger;

    private static readonly (string Key, string Name, AgentSectionKey Section)[] AgentFlags =
    [
        ("agent_core",          "Agent Core",                  AgentSectionKey.AgentCore),
        ("personal_finance",    "Personal Finance Context",    AgentSectionKey.PersonalFinance),
        ("macro_insights",      "Macro Insights",              AgentSectionKey.MacroInsights),
        ("net_worth_analysis",  "Net Worth Growth Analysis",   AgentSectionKey.NetWorthAnalysis),
        ("liability_review",    "Liability & Asset Review",    AgentSectionKey.LiabilityReview),
        ("chat",                "Conversational Chat",         AgentSectionKey.ConversationalChat),
        ("scenario_exploration","Scenario Exploration",        AgentSectionKey.ScenarioExploration),
        ("alerts_nudges",       "Alerts & Nudges",             AgentSectionKey.AlertsNudges),
        ("explanation_mode",    "Explanation / Audit Mode",    AgentSectionKey.ExplanationMode),
    ];

    private static readonly string[] OwnerPermissions =
    [
        "dashboard:read",
        "accounts:read", "accounts:write",
        "transactions:read", "transactions:write",
        "investments:read", "investments:write",
        "budgets:read", "budgets:write",
        "reports:read",
        "members:manage", "entity:manage",
        "agent:read", "agent:chat", "agent:admin:flags",
    ];

    private static readonly string[] EditorPermissions =
    [
        "dashboard:read",
        "accounts:read", "accounts:write",
        "transactions:read", "transactions:write",
        "investments:read", "investments:write",
        "budgets:read", "budgets:write",
        "reports:read",
        "agent:read", "agent:chat",
    ];

    private static readonly string[] ViewerPermissions =
    [
        "dashboard:read",
        "accounts:read",
        "transactions:read",
        "investments:read",
        "budgets:read",
        "reports:read",
        "agent:read",
    ];

    public SystemDataSeeder(IServiceProvider services, ILogger<SystemDataSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await SeedAgentFeatureFlagsAsync(db, ct);
            await SeedSystemRolesAsync(db, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemDataSeeder failed");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task SeedAgentFeatureFlagsAsync(AppDbContext db, CancellationToken ct)
    {
        var existingKeys = (await db.AgentFeatureFlags
            .Where(f => !f.IsDeleted)
            .Select(f => f.Key)
            .ToListAsync(ct))
            .ToHashSet();

        var toAdd = AgentFlags
            .Where(f => !existingKeys.Contains(f.Key))
            .Select(f => new AgentFeatureFlag
            {
                Key = f.Key,
                Name = f.Name,
                SectionKey = f.Section,
                IsEnabled = true,
                TenantId = string.Empty,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            db.AgentFeatureFlags.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedSystemRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var entityIds = await db.Entities
            .Where(e => !e.IsDeleted)
            .Select(e => new { e.Id, e.TenantId })
            .ToListAsync(ct);

        var existingRoles = await db.EntityCustomRoles
            .Where(r => r.IsSystem && !r.IsDeleted)
            .Select(r => new { r.EntityId, r.Name, r.Id })
            .ToListAsync(ct);

        var existingRoleKeys = existingRoles
            .Select(r => (r.EntityId, r.Name))
            .ToHashSet();

        var newRoles = new List<EntityCustomRole>();

        foreach (var entity in entityIds)
        {
            foreach (var roleName in new[] { "Owner", "Editor", "Viewer" })
            {
                if (!existingRoleKeys.Contains((entity.Id, roleName)))
                {
                    newRoles.Add(new EntityCustomRole
                    {
                        EntityId = entity.Id,
                        TenantId = entity.TenantId,
                        Name = roleName,
                        IsSystem = true,
                    });
                }
            }
        }

        if (newRoles.Count > 0)
        {
            db.EntityCustomRoles.AddRange(newRoles);
            await db.SaveChangesAsync(ct);
        }

        // Seed permissions for all system roles (new and existing that may be missing keys)
        var allSystemRoles = await db.EntityCustomRoles
            .Where(r => r.IsSystem && !r.IsDeleted)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);

        var existingPermissions = await db.EntityRolePermissions
            .Where(p => allSystemRoles.Select(r => r.Id).Contains(p.RoleId))
            .Select(p => new { p.RoleId, p.PermissionKey })
            .ToListAsync(ct);

        var existingPermissionSet = existingPermissions
            .Select(p => (p.RoleId, p.PermissionKey))
            .ToHashSet();

        var newPermissions = new List<EntityRolePermission>();

        foreach (var role in allSystemRoles)
        {
            var keys = role.Name switch
            {
                "Owner"  => OwnerPermissions,
                "Editor" => EditorPermissions,
                "Viewer" => ViewerPermissions,
                _        => Array.Empty<string>(),
            };

            foreach (var key in keys)
            {
                if (!existingPermissionSet.Contains((role.Id, key)))
                {
                    newPermissions.Add(new EntityRolePermission
                    {
                        RoleId = role.Id,
                        PermissionKey = key,
                    });
                }
            }
        }

        if (newPermissions.Count > 0)
        {
            db.EntityRolePermissions.AddRange(newPermissions);
            await db.SaveChangesAsync(ct);
        }
    }
}
