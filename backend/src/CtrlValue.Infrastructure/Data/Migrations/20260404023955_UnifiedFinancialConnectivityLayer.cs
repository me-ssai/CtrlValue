using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedFinancialConnectivityLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "basiq_account");

            migrationBuilder.DropTable(
                name: "plaid_account");

            migrationBuilder.DropTable(
                name: "basiq_connection");

            migrationBuilder.DropTable(
                name: "plaid_connection");

            migrationBuilder.DropColumn(
                name: "PlaidAccountId",
                table: "account");

            migrationBuilder.RenameColumn(
                name: "PlaidSyncEnabled",
                table: "account",
                newName: "IsSyncEnabled");

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectionId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalTransactionId",
                table: "txn",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionProvider",
                table: "account",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "financial_connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderConnectionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedCredential = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstitutionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstitutionLogoUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false, defaultValue: "AU"),
                    ConsentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_connection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_connection_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connected_account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OfficialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Mask = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subtype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_connected_account", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connected_account_account_LinkedAccountId",
                        column: x => x.LinkedAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_connected_account_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_connected_account_financial_connection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "financial_connection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "connection_sync_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountsSynced = table.Column<int>(type: "integer", nullable: true),
                    TransactionsStaged = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_sync_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_connection_sync_log_financial_connection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "financial_connection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connected_account_ConnectionId_ExternalAccountId",
                table: "connected_account",
                columns: new[] { "ConnectionId", "ExternalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connected_account_EntityId",
                table: "connected_account",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_connected_account_LinkedAccountId",
                table: "connected_account",
                column: "LinkedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_connection_sync_log_ConnectionId",
                table: "connection_sync_log",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_financial_connection_EntityId_ProviderConnectionId",
                table: "financial_connection",
                columns: new[] { "EntityId", "ProviderConnectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connected_account");

            migrationBuilder.DropTable(
                name: "connection_sync_log");

            migrationBuilder.DropTable(
                name: "financial_connection");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ExternalTransactionId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ConnectionProvider",
                table: "account");

            migrationBuilder.RenameColumn(
                name: "IsSyncEnabled",
                table: "account",
                newName: "PlaidSyncEnabled");

            migrationBuilder.AddColumn<string>(
                name: "PlaidAccountId",
                table: "account",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "basiq_connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasiqConnectionId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BasiqUserId = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConsentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstitutionLogo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InstitutionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "plaid_connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConsentExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InstitutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstitutionLogo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InstitutionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plaid_connection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plaid_connection_entity_EntityId",
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
                    LinkedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BasiqAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "plaid_account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlaidConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Mask = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OfficialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PlaidAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subtype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plaid_account", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plaid_account_account_LinkedAccountId",
                        column: x => x.LinkedAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_plaid_account_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plaid_account_plaid_connection_PlaidConnectionId",
                        column: x => x.PlaidConnectionId,
                        principalTable: "plaid_connection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateIndex(
                name: "IX_plaid_account_EntityId",
                table: "plaid_account",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_plaid_account_LinkedAccountId",
                table: "plaid_account",
                column: "LinkedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_plaid_account_PlaidConnectionId_PlaidAccountId",
                table: "plaid_account",
                columns: new[] { "PlaidConnectionId", "PlaidAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plaid_connection_EntityId",
                table: "plaid_connection",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_plaid_connection_ItemId",
                table: "plaid_connection",
                column: "ItemId",
                unique: true);
        }
    }
}
