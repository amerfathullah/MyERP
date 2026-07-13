using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeferredRevenueAndAmendment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Sal_SalesInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DeferredRevenueAccountId",
                table: "Sal_SalesInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableDeferredRevenue",
                table: "Sal_SalesInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceEndDate",
                table: "Sal_SalesInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceStartDate",
                table: "Sal_SalesInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DeferredRevenueAccountId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "EnableDeferredRevenue",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceEndDate",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceStartDate",
                table: "Sal_SalesInvoiceItems");
        }
    }
}
