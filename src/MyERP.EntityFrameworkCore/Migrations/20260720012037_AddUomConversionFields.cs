using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddUomConversionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Sal_SalesOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Sal_SalesOrderItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Sal_SalesInvoiceItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Sal_SalesInvoiceItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Sal_DeliveryNoteItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Sal_DeliveryNoteItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Pur_PurchaseReceiptItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Pur_PurchaseReceiptItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Pur_PurchaseOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Pur_PurchaseOrderItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Pur_PurchaseInvoiceItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Pur_PurchaseInvoiceItems",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Pur_PurchaseInvoiceItems");
        }
    }
}
