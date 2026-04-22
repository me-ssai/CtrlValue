using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStartingBalanceToAccount : Migration
    {
        /// <inheritdoc />
         protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOffsetAccount",
                table: "account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StartingBalance",
                table: "account",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartingBalanceDate",
                table: "account",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // OpeningBalance = 9
            migrationBuilder.Sql(@"
                UPDATE account
                SET
                    ""StartingBalance""     = ""CurrentBalance"",
                    ""StartingBalanceDate"" = COALESCE(
                        (SELECT MAX(t.""TxnTime"")
                        FROM txn t
                        WHERE t.""AccountId"" = account.""Id""
                        AND t.""IsDeleted"" = false
                        AND t.""TxnType"" <> 9),
                        account.""CreatedAt""
                    )
                WHERE account.""IsDeleted"" = false;
            ");

            // CapitalDeposit = 7
            migrationBuilder.Sql(@"
                UPDATE txn
                SET ""IsDeleted"" = true, ""UpdatedAt"" = NOW()
                WHERE ""IsDeleted"" = false
                AND ""TxnType"" = 7
                AND ""Description"" = 'Initial Balance / Baseline Valuation'
                AND ""Source"" = 'MANUAL';
            ");

            // OpeningBalance = 9
            migrationBuilder.Sql(@"
                INSERT INTO txn (
                    ""Id"", ""EntityId"", ""AccountId"", ""TxnTime"",
                    ""Description"", ""Amount"", ""Currency"",
                    ""TxnType"", ""Direction"",
                    ""IsReconciled"", ""Source"", ""IsDeleted"",
                    ""CreatedAt"", ""UpdatedAt"", ""TenantId""
                )
                SELECT
                    gen_random_uuid(),
                    a.""EntityId"",
                    a.""Id"",
                    a.""StartingBalanceDate"",
                    'Opening Balance as of ' || TO_CHAR(a.""StartingBalanceDate"" AT TIME ZONE 'UTC', 'YYYY-MM-DD'),
                    a.""StartingBalance"",
                    a.""Currency"",
                    9,
                    'Inflow',
                    true,
                    'SYSTEM',
                    false,
                    NOW(),
                    NULL,
                    COALESCE(a.""TenantId"", 'default')
                FROM account a
                WHERE a.""IsDeleted"" = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOffsetAccount",
                table: "account");

            migrationBuilder.DropColumn(
                name: "StartingBalance",
                table: "account");

            migrationBuilder.DropColumn(
                name: "StartingBalanceDate",
                table: "account");
        }
    }
}
