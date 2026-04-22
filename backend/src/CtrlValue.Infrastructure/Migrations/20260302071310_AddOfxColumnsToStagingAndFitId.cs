using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfxColumnsToStagingAndFitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Impo~",
                table: "imported_transactions_files_staging");

            migrationBuilder.AddColumn<string>(
                name: "FitId",
                table: "txn",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "imported_transactions_files_staging",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "imported_transactions_files_staging",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfxTrnType",
                table: "imported_transactions_files_staging",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TenantId_EntityId_Accou~",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "EntityId", "AccountId", "ImportedTransactionsFileId", "Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_staging_fitid_unique",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "EntityId", "AccountId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_imported_transactions_files_staging_TenantId_EntityId_Accou~",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropIndex(
                name: "IX_staging_fitid_unique",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropColumn(
                name: "FitId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropColumn(
                name: "OfxTrnType",
                table: "imported_transactions_files_staging");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Impo~",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "AccountId", "ImportedTransactionsFileId", "Hash" });
        }
    }
}
