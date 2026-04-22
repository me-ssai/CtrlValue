using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDetailsAndRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExtraRepayment",
                table: "txn",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "loan_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    OffsetAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    RateType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FixedRateExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RepaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LoanTermMonths = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextPaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsInterestOnly = table.Column<bool>(type: "boolean", nullable: false),
                    RedrawAvailable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_details_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loan_details_account_OffsetAccountId",
                        column: x => x.OffsetAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_loan_details_account_PropertyAccountId",
                        column: x => x.PropertyAccountId,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "loan_rate_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanDetailsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_rate_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_rate_history_loan_details_LoanDetailsId",
                        column: x => x.LoanDetailsId,
                        principalTable: "loan_details",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_details_AccountId",
                table: "loan_details",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_details_OffsetAccountId",
                table: "loan_details",
                column: "OffsetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_details_PropertyAccountId",
                table: "loan_details",
                column: "PropertyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_rate_history_LoanDetailsId",
                table: "loan_rate_history",
                column: "LoanDetailsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_rate_history");

            migrationBuilder.DropTable(
                name: "loan_details");

            migrationBuilder.DropColumn(
                name: "IsExtraRepayment",
                table: "txn");
        }
    }
}
