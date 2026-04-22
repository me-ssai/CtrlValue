using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SingleAccountModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_imported_transactions_files_staging_account_FromAccountId",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropForeignKey(
                name: "FK_imported_transactions_files_staging_account_ToAccountId",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_account_FromAccountId",
                table: "txn");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_account_ToAccountId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_FromAccountId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_ToAccountId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_imported_transactions_files_staging_FromAccountId",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropColumn(
                name: "FromAccountId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "ToAccountId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "FromAccountId",
                table: "imported_transactions_files_staging");

            migrationBuilder.RenameColumn(
                name: "ToAccountId",
                table: "imported_transactions_files_staging",
                newName: "CounterAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_imported_transactions_files_staging_ToAccountId",
                table: "imported_transactions_files_staging",
                newName: "IX_imported_transactions_files_staging_CounterAccountId");

            migrationBuilder.AlterColumn<string>(
                name: "Direction",
                table: "txn",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "txn",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_txn_AccountId",
                table: "txn",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_imported_transactions_files_staging_account_CounterAccountId",
                table: "imported_transactions_files_staging",
                column: "CounterAccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_txn_account_AccountId",
                table: "txn",
                column: "AccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_imported_transactions_files_staging_account_CounterAccountId",
                table: "imported_transactions_files_staging");

            migrationBuilder.DropForeignKey(
                name: "FK_txn_account_AccountId",
                table: "txn");

            migrationBuilder.DropIndex(
                name: "IX_txn_AccountId",
                table: "txn");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "txn");

            migrationBuilder.RenameColumn(
                name: "CounterAccountId",
                table: "imported_transactions_files_staging",
                newName: "ToAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_imported_transactions_files_staging_CounterAccountId",
                table: "imported_transactions_files_staging",
                newName: "IX_imported_transactions_files_staging_ToAccountId");

            migrationBuilder.AlterColumn<string>(
                name: "Direction",
                table: "txn",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<Guid>(
                name: "FromAccountId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToAccountId",
                table: "txn",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromAccountId",
                table: "imported_transactions_files_staging",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_txn_FromAccountId",
                table: "txn",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_txn_ToAccountId",
                table: "txn",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_imported_transactions_files_staging_FromAccountId",
                table: "imported_transactions_files_staging",
                column: "FromAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_imported_transactions_files_staging_account_FromAccountId",
                table: "imported_transactions_files_staging",
                column: "FromAccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_imported_transactions_files_staging_account_ToAccountId",
                table: "imported_transactions_files_staging",
                column: "ToAccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
        }
    }
}
