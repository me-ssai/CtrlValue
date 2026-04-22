using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CtrlValue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBondEtfMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouponFrequency",
                table: "instrument",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CouponRate",
                table: "instrument",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditRating",
                table: "instrument",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributionFrequency",
                table: "instrument",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistributionYield",
                table: "instrument",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpenseRatio",
                table: "instrument",
                type: "numeric(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FaceValue",
                table: "instrument",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                table: "instrument",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "instrument",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaturityDate",
                table: "instrument",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnderlyingIndex",
                table: "instrument",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CouponFrequency",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "CouponRate",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "CreditRating",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "DistributionFrequency",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "DistributionYield",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "ExpenseRatio",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "FaceValue",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "MaturityDate",
                table: "instrument");

            migrationBuilder.DropColumn(
                name: "UnderlyingIndex",
                table: "instrument");
        }
    }
}
