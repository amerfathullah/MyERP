using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessLogicColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderItemId",
                table: "Sal_SalesInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "Sal_Customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderItemId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockQueue",
                table: "Inv_StockLedgerEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccountsFrozenTillDate",
                table: "AppCompanies",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockFrozenUpto",
                table: "AppCompanies",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderItemId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "StockQueue",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "AccountsFrozenTillDate",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "StockFrozenUpto",
                table: "AppCompanies");
        }
    }
}
