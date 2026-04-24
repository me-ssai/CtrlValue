using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentIsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "instrument",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "instrument");
        }
    }
}
