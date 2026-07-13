using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDefaultAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccumulatedDepreciationAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBankAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExpenseAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultIncomeAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInventoryAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPayableAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultReceivableAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepreciationExpenseAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciationAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultBankAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultIncomeAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultInventoryAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultPayableAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultReceivableAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DepreciationExpenseAccountId",
                table: "AppCompanies");
        }
    }
}
