using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ComprehensiveSchemaEvolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_positions_users_UserId",
                table: "positions");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_users_UserId",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "income_streams");

            migrationBuilder.DropTable(
                name: "liabilities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transactions",
                table: "transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_positions",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "AssetType",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "CostBasis",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "CurrentPrice",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "positions");

            migrationBuilder.RenameTable(
                name: "transactions",
                newName: "txn");

            migrationBuilder.RenameTable(
                name: "positions",
                newName: "position");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "txn",
                newName: "EntityId");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "txn",
                newName: "TxnType");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "txn",
                newName: "TxnTime");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_UserId",
                table: "txn",
                newName: "IX_txn_EntityId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "position",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "Symbol",
                table: "position",
                newName: "Unit");

            migrationBuilder.RenameIndex(
                name: "IX_positions_UserId",
                table: "position",
                newName: "IX_position_AccountId");

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailConfirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "txn",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "txn",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fees",
                table: "txn",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromAccountId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstrumentId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReconciled",
                table: "txn",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxDeductible",
                table: "txn",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Merchant",
                table: "txn",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "txn",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptUrl",
                table: "txn",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedTxnId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "txn",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToAccountId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "txn",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostBasisTotal",
                table: "position",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstrumentId",
                table: "position",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "position",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_txn",
                table: "txn",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_position",
                table: "position",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "entity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "instrument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstrumentType = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exchange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instrument", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    AssetClass = table.Column<int>(type: "integer", nullable: true),
                    LiquidityClass = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Institution = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryType = table.Column<int>(type: "integer", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.Id);
                    table.ForeignKey(
                        name: "FK_category_category_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_category_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entity_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entity_users_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_entity_users_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    ClosePrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    HighPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LowPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Volume = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_history_instrument_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "instrument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "depreciation_schedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsefulLifeYears = table.Column<int>(type: "integer", nullable: true),
                    SalvageValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AnnualDepreciationRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_depreciation_schedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_depreciation_schedule_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "valuation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AsOfDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_valuation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_valuation_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_budget_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_txn_CategoryId",
                table: "txn",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_txn_FromAccountId",
                table: "txn",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_txn_InstrumentId",
                table: "txn",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_txn_ToAccountId",
                table: "txn",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_position_InstrumentId",
                table: "position",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_account_EntityId",
                table: "account",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_CategoryId",
                table: "budget",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_EntityId",
                table: "budget",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_category_EntityId",
                table: "category",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_category_ParentCategoryId",
                table: "category",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_depreciation_schedule_AccountId",
                table: "depreciation_schedule",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_users_EntityId_UserId",
                table: "entity_users",
                columns: new[] { "EntityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_users_UserId",
                table: "entity_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_instrument_Symbol",
                table: "instrument",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_history_InstrumentId_AsOfDate",
                table: "price_history",
                columns: new[] { "InstrumentId", "AsOfDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_valuation_AccountId",
                table: "valuation",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_position_account_AccountId",
                table: "position",
                column: "AccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_position_instrument_InstrumentId",
                table: "position",
                column: "InstrumentId",
                principalTable: "instrument",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_account_FromAccountId",
                table: "txn",
                column: "FromAccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_account_ToAccountId",
                table: "txn",
                column: "ToAccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_category_CategoryId",
                table: "txn",
                column: "CategoryId",
                principalTable: "category",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_entity_EntityId",
                table: "txn",
                column: "EntityId",
                principalTable: "entity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_instrument_InstrumentId",
                table: "txn",
                column: "InstrumentId",
                principalTable: "instrument",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_position_account_AccountId",
                table: "position");

            migrationBuilder.DropForeignKey(
                name: "FK_position_instrument_InstrumentId",
                table: "position");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_account_FromAccountId",
                table: "txn");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_account_ToAccountId",
                table: "txn");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_category_CategoryId",
                table: "txn");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_entity_EntityId",
                table: "txn");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_instrument_InstrumentId",
                table: "txn");

            migrationBuilder.DropTable(
                name: "budget");

            migrationBuilder.DropTable(
                name: "depreciation_schedule");

            migrationBuilder.DropTable(
                name: "entity_users");

            migrationBuilder.DropTable(
                name: "price_history");

            migrationBuilder.DropTable(
                name: "valuation");

            migrationBuilder.DropTable(
                name: "category");

            migrationBuilder.DropTable(
                name: "instrument");

            migrationBuilder.DropTable(
                name: "account");

            migrationBuilder.DropTable(
                name: "entity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_txn",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_CategoryId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_FromAccountId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_InstrumentId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_ToAccountId",
                table: "txn");

            migrationBuilder.DropPrimaryKey(
                name: "PK_position",
                table: "position");

            migrationBuilder.DropIndex(
                name: "IX_position_InstrumentId",
                table: "position");

            migrationBuilder.DropColumn(
                name: "IsEmailConfirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Fees",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "FromAccountId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "InstrumentId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "IsReconciled",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "IsTaxDeductible",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Merchant",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ReceiptUrl",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "RelatedTxnId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "CostBasisTotal",
                table: "position");

            migrationBuilder.DropColumn(
                name: "InstrumentId",
                table: "position");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "position");

            migrationBuilder.RenameTable(
                name: "txn",
                newName: "transactions");

            migrationBuilder.RenameTable(
                name: "position",
                newName: "positions");

            migrationBuilder.RenameColumn(
                name: "TxnType",
                table: "transactions",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "TxnTime",
                table: "transactions",
                newName: "Date");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "transactions",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_txn_EntityId",
                table: "transactions",
                newName: "IX_transactions_UserId");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "positions",
                newName: "Symbol");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "positions",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_position_AccountId",
                table: "positions",
                newName: "IX_positions_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "transactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AssetType",
                table: "positions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostBasis",
                table: "positions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "positions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentPrice",
                table: "positions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "positions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_transactions",
                table: "transactions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_positions",
                table: "positions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "income_streams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_income_streams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_income_streams_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "liabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    InterestRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liabilities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assets_UserId",
                table: "assets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_income_streams_UserId",
                table: "income_streams",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_liabilities_UserId",
                table: "liabilities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_positions_users_UserId",
                table: "positions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_users_UserId",
                table: "transactions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
