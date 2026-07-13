using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderIdPRItemIdCurrencyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptItemId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkOrderId",
                table: "Inv_StockEntries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseReceiptItemId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "WorkOrderId",
                table: "Inv_StockEntries");
        }
    }
}
