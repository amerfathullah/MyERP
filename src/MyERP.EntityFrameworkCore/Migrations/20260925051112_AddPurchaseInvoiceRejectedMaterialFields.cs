using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInvoiceRejectedMaterialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BillsRejectedQuantity",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BillsRejectedQuantity",
                table: "Pur_PurchaseInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                table: "Pur_PurchaseInvoiceItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQty",
                table: "Pur_PurchaseInvoiceItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillsRejectedQuantity",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BillsRejectedQuantity",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "RejectedQty",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseInvoiceItems");
        }
    }
}
