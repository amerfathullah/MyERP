using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInterCompanyInvoiceItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceItemId",
                table: "Sal_SalesInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesInvoiceItemId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesInvoiceItems_PurchaseInvoiceItemId",
                table: "Sal_SalesInvoiceItems",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseInvoiceItems_SalesInvoiceItemId",
                table: "Pur_PurchaseInvoiceItems",
                column: "SalesInvoiceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sal_SalesInvoiceItems_PurchaseInvoiceItemId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseInvoiceItems_SalesInvoiceItemId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceItemId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "SalesInvoiceItemId",
                table: "Pur_PurchaseInvoiceItems");
        }
    }
}
