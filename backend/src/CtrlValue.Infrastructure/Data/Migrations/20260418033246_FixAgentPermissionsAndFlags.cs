using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAgentPermissionsAndFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed agent permissions for all existing Owner roles.
            // These were omitted from the original AddCustomRolePermissions migration.
            migrationBuilder.Sql(@"
                INSERT INTO entity_role_permissions (""RoleId"", ""PermissionKey"")
                SELECT r.""Id"", p.key
                FROM entity_custom_roles r
                CROSS JOIN (VALUES ('agent:read'), ('agent:chat'), ('agent:admin:flags')) AS p(key)
                WHERE r.""Name"" = 'Owner' AND r.""IsSystem"" = true
                ON CONFLICT DO NOTHING;
            ");

            // Enable all agent feature flags. They were seeded as disabled (IsEnabled = false)
            // in AddCtrlValueAgentModule, which blocked the AgentCore master switch and every
            // section beneath it. This makes all sections accessible by default.
            migrationBuilder.Sql(@"
                UPDATE agent_feature_flags SET ""IsEnabled"" = true WHERE ""IsDeleted"" = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM entity_role_permissions
                WHERE ""PermissionKey"" IN ('agent:read', 'agent:chat', 'agent:admin:flags');
            ");

            migrationBuilder.Sql(@"
                UPDATE agent_feature_flags SET ""IsEnabled"" = false WHERE ""IsDeleted"" = false;
            ");
        }
    }
}
