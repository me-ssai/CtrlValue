using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStagingDuplicateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Hash",
                table: "imported_transactions_files_staging");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Impo~",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "AccountId", "ImportedTransactionsFileId", "Hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Impo~",
                table: "imported_transactions_files_staging");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_TenantId_AccountId_Hash",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "AccountId", "Hash" },
                unique: true);
        }
    }
}
