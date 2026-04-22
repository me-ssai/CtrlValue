using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTickerInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── IsDefault on instrument ──────────────────────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "instrument",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // ── platform_integration ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "platform_integration",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiKey       = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled    = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastUsedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId     = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted    = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_integration", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_integration_IntegrationType",
                table: "platform_integration",
                column: "IntegrationType",
                unique: true);

            // ── entity_default_ticker ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "entity_default_ticker",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId     = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId     = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted    = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_default_ticker", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entity_default_ticker_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entity_default_ticker_instrument_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instrument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_default_ticker_EntityId_InstrumentId",
                table: "entity_default_ticker",
                columns: new[] { "EntityId", "InstrumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "entity_default_ticker");
            migrationBuilder.DropTable(name: "platform_integration");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "instrument");
        }
    }
}
