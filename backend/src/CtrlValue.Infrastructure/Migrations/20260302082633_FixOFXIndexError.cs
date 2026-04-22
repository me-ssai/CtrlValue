using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixOFXIndexError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staging_fitid_unique",
                table: "imported_transactions_files_staging");

            migrationBuilder.CreateIndex(
                name: "IX_staging_fitid",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "EntityId", "AccountId", "ExternalId" },
                filter: "\"ExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staging_fitid",
                table: "imported_transactions_files_staging");

            migrationBuilder.CreateIndex(
                name: "IX_staging_fitid_unique",
                table: "imported_transactions_files_staging",
                columns: new[] { "TenantId", "EntityId", "AccountId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL");
        }
    }
}
