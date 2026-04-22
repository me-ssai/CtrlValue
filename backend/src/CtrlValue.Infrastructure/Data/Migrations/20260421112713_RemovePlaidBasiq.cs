using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaidBasiq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft-delete any existing BASIQ entity integration rows — provider no longer supported.
            migrationBuilder.Sql(
                "UPDATE entity_integration " +
                "SET \"IsDeleted\" = true, \"UpdatedAt\" = NOW() " +
                "WHERE \"IntegrationType\" = 'BASIQ' AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE entity_integration " +
                "SET \"IsDeleted\" = false, \"UpdatedAt\" = NOW() " +
                "WHERE \"IntegrationType\" = 'BASIQ' AND \"IsDeleted\" = true");
        }
    }
}
