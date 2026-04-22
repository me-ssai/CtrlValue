using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQifImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "txn",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "txn",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "txn",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MANUAL");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTransactionsFileId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferGroupId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "imported_transactions_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AllowDuplicates = table.Column<bool>(type: "boolean", nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    DuplicateRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorRows = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imported_transactions_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "entity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "imported_transactions_files_staging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedTransactionsFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountRaw = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imported_transactions_files_staging", x => x.Id);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_staging_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_staging_account_FromAccountId",
                        column: x => x.FromAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_staging_account_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_staging_category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_imported_transactions_files_staging_imported_transactions_f~",
                        column: x => x.ImportedTransactionsFileId,
                        principalTable: "imported_transactions_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_AccountId",
                table: "imported_transactions_files",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_EntityId",
                table: "imported_transactions_files",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_TenantId",
                table: "imported_transactions_files",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_AccountId",
                table: "imported_transactions_files_staging",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_CategoryId",
                table: "imported_transactions_files_staging",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_EntityId",
                table: "imported_transactions_files_staging",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_FromAccountId",
                table: "imported_transactions_files_staging",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_ImportedTransactionsFil~",
                table: "imported_transactions_files_staging",
                column: "ImportedTransactionsFileId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_Status",
                table: "imported_transactions_files_staging",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Hash",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "AccountId", "Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_ToAccountId",
                table: "imported_transactions_files_staging",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TransactionDate",
                table: "imported_transactions_files_staging",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imported_transactions_files_staging");

            migrationBuilder.DropTable(
                name: "imported_transactions_files");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "SourceTransactionsFileId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "TransferGroupId",
                table: "txn");
        }
    }
}
