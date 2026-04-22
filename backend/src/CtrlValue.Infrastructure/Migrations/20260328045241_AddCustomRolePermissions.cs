using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add CustomRoleId as nullable initially (populated via data migration below)
            migrationBuilder.AddColumn<Guid>(
                name: "CustomRoleId",
                table: "entity_users",
                type: "uuid",
                nullable: true);

            // Step 2: Create entity_custom_roles table
            migrationBuilder.CreateTable(
                name: "entity_custom_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_custom_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entity_custom_roles_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Step 3: Create entity_role_permissions table
            migrationBuilder.CreateTable(
                name: "entity_role_permissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_role_permissions", x => new { x.RoleId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_entity_role_permissions_entity_custom_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "entity_custom_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Step 4: Indexes
            migrationBuilder.CreateIndex(
                name: "IX_entity_custom_roles_EntityId_Name",
                table: "entity_custom_roles",
                columns: new[] { "EntityId", "Name" },
                unique: true);

            // Step 5: Seed system roles (Owner/Editor/Viewer) for every existing entity
            migrationBuilder.Sql(@"
                INSERT INTO entity_custom_roles (""Id"", ""EntityId"", ""TenantId"", ""Name"", ""IsSystem"", ""CreatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid(), e.""Id"", e.""TenantId"", 'Owner',  true, NOW(), false FROM entity e WHERE e.""IsDeleted"" = false
                UNION ALL
                SELECT gen_random_uuid(), e.""Id"", e.""TenantId"", 'Editor', true, NOW(), false FROM entity e WHERE e.""IsDeleted"" = false
                UNION ALL
                SELECT gen_random_uuid(), e.""Id"", e.""TenantId"", 'Viewer', true, NOW(), false FROM entity e WHERE e.""IsDeleted"" = false;
            ");

            // Step 6: Seed permissions for Owner (all permissions)
            migrationBuilder.Sql(@"
                INSERT INTO entity_role_permissions (""RoleId"", ""PermissionKey"")
                SELECT r.""Id"", p.key
                FROM entity_custom_roles r
                CROSS JOIN (VALUES
                    ('dashboard:read'),
                    ('accounts:read'),      ('accounts:write'),
                    ('transactions:read'),  ('transactions:write'),
                    ('investments:read'),   ('investments:write'),
                    ('budgets:read'),       ('budgets:write'),
                    ('reports:read'),
                    ('members:manage'),     ('entity:manage')
                ) AS p(key)
                WHERE r.""Name"" = 'Owner' AND r.""IsSystem"" = true;
            ");

            // Step 7: Seed permissions for Editor (read + write, no manage)
            migrationBuilder.Sql(@"
                INSERT INTO entity_role_permissions (""RoleId"", ""PermissionKey"")
                SELECT r.""Id"", p.key
                FROM entity_custom_roles r
                CROSS JOIN (VALUES
                    ('dashboard:read'),
                    ('accounts:read'),      ('accounts:write'),
                    ('transactions:read'),  ('transactions:write'),
                    ('investments:read'),   ('investments:write'),
                    ('budgets:read'),       ('budgets:write'),
                    ('reports:read')
                ) AS p(key)
                WHERE r.""Name"" = 'Editor' AND r.""IsSystem"" = true;
            ");

            // Step 8: Seed permissions for Viewer (read only)
            migrationBuilder.Sql(@"
                INSERT INTO entity_role_permissions (""RoleId"", ""PermissionKey"")
                SELECT r.""Id"", p.key
                FROM entity_custom_roles r
                CROSS JOIN (VALUES
                    ('dashboard:read'),
                    ('accounts:read'),
                    ('transactions:read'),
                    ('investments:read'),
                    ('budgets:read'),
                    ('reports:read')
                ) AS p(key)
                WHERE r.""Name"" = 'Viewer' AND r.""IsSystem"" = true;
            ");

            // Step 9: Map each EntityUser to the matching system role
            // EntityRole enum: 0 = OWNER, 1 = VIEWER, 2 = EDITOR
            migrationBuilder.Sql(@"
                UPDATE entity_users eu
                SET ""CustomRoleId"" = (
                    SELECT r.""Id""
                    FROM entity_custom_roles r
                    WHERE r.""EntityId"" = eu.""EntityId""
                      AND r.""IsSystem"" = true
                      AND r.""Name"" = CASE eu.""Role""
                          WHEN 0 THEN 'Owner'
                          WHEN 1 THEN 'Viewer'
                          WHEN 2 THEN 'Editor'
                          ELSE 'Viewer'
                      END
                    LIMIT 1
                );
            ");

            // Step 9b: Delete any entity_users rows that still have NULL CustomRoleId.
            // This happens for rows whose entity was soft-deleted — Step 5 only seeds roles
            // for non-deleted entities, so these rows have no custom role to map to.
            // Since their entity is gone, there is no valid role to assign; hard-delete them.
            migrationBuilder.Sql(@"
                DELETE FROM entity_users WHERE ""CustomRoleId"" IS NULL;
            ");

            // Step 10: Make CustomRoleId NOT NULL now that all rows are populated
            migrationBuilder.AlterColumn<Guid>(
                name: "CustomRoleId",
                table: "entity_users",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Step 11: Add FK and index
            migrationBuilder.CreateIndex(
                name: "IX_entity_users_CustomRoleId",
                table: "entity_users",
                column: "CustomRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_entity_users_entity_custom_roles_CustomRoleId",
                table: "entity_users",
                column: "CustomRoleId",
                principalTable: "entity_custom_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Step 12: Drop the old Role column (no longer needed)
            migrationBuilder.DropColumn(
                name: "Role",
                table: "entity_users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_entity_users_entity_custom_roles_CustomRoleId",
                table: "entity_users");

            migrationBuilder.DropTable(
                name: "entity_role_permissions");

            migrationBuilder.DropTable(
                name: "entity_custom_roles");

            migrationBuilder.DropIndex(
                name: "IX_entity_users_CustomRoleId",
                table: "entity_users");

            migrationBuilder.DropColumn(
                name: "CustomRoleId",
                table: "entity_users");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "entity_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
