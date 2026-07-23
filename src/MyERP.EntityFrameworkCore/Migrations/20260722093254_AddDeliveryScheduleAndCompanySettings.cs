using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryScheduleAndCompanySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoExchangeRateRevaluation",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BookAdvancePaymentsInSeparatePartyAccount",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAdvancePaidAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAdvanceReceivedAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePerpetualInventory",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OverBillingAllowance",
                table: "AppCompanies",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverDeliveryReceiptAllowance",
                table: "AppCompanies",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReportingCurrency",
                table: "AppCompanies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoundOffAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoundOffForOpeningAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockDeliveredButNotBilledAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockReceivedButNotBilledAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoExchangeRateRevaluation",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "BookAdvancePaymentsInSeparatePartyAccount",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultAdvancePaidAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultAdvanceReceivedAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "EnablePerpetualInventory",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "OverBillingAllowance",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "OverDeliveryReceiptAllowance",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ReportingCurrency",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "RoundOffAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "RoundOffForOpeningAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "StockDeliveredButNotBilledAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "StockReceivedButNotBilledAccountId",
                table: "AppCompanies");
        }
    }
}
