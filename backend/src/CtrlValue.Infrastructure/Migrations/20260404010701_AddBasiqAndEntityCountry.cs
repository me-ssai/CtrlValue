using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBasiqAndEntityCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "entity",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "AU");

            migrationBuilder.CreateTable(
                name: "basiq_connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasiqUserId = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    BasiqConnectionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstitutionLogo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    ConsentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_basiq_connection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_basiq_connection_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "basiq_account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BasiqConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasiqAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_basiq_account", x => x.Id);
                    table.ForeignKey(
                        name: "FK_basiq_account_account_LinkedAccountId",
                        column: x => x.LinkedAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_basiq_account_basiq_connection_BasiqConnectionId",
                        column: x => x.BasiqConnectionId,
                        principalTable: "basiq_connection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_basiq_account_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_basiq_account_BasiqConnectionId_BasiqAccountId",
                table: "basiq_account",
                columns: new[] { "BasiqConnectionId", "BasiqAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_basiq_account_EntityId",
                table: "basiq_account",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_basiq_account_LinkedAccountId",
                table: "basiq_account",
                column: "LinkedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_basiq_connection_EntityId_BasiqConnectionId",
                table: "basiq_connection",
                columns: new[] { "EntityId", "BasiqConnectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "basiq_account");

            migrationBuilder.DropTable(
                name: "basiq_connection");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "entity");
        }
    }
}
