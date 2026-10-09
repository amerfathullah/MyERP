using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceBillingDatesAndCustomerPoNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPoNumber",
                table: "Sal_SalesInvoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FromDate",
                table: "Sal_SalesInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDate",
                table: "Sal_SalesInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FromDate",
                table: "Pur_PurchaseInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDate",
                table: "Pur_PurchaseInvoices",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPoNumber",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "Pur_PurchaseInvoices");
        }
    }
}
