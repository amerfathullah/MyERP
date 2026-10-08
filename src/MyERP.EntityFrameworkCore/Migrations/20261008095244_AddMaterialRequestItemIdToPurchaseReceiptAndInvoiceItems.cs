using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialRequestItemIdToPurchaseReceiptAndInvoiceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseReceipts",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<bool>(
                name: "UseTransactionDateExchangeRate",
                table: "Pur_PurchaseReceipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MaterialRequestItemId",
                table: "Pur_PurchaseReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<bool>(
                name: "UseTransactionDateExchangeRate",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MaterialRequestItemId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubAssemblyWarehouseId",
                table: "Mfg_ProductionPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceiptItems_MaterialRequestItemId",
                table: "Pur_PurchaseReceiptItems",
                column: "MaterialRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseInvoiceItems_MaterialRequestItemId",
                table: "Pur_PurchaseInvoiceItems",
                column: "MaterialRequestItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlans_TenantId_SubAssemblyWarehouseId",
                table: "Mfg_ProductionPlans",
                columns: new[] { "TenantId", "SubAssemblyWarehouseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseReceiptItems_MaterialRequestItemId",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseInvoiceItems_MaterialRequestItemId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Mfg_ProductionPlans_TenantId_SubAssemblyWarehouseId",
                table: "Mfg_ProductionPlans");

            migrationBuilder.DropColumn(
                name: "UseTransactionDateExchangeRate",
                table: "Pur_PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "MaterialRequestItemId",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "UseTransactionDateExchangeRate",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "MaterialRequestItemId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "SubAssemblyWarehouseId",
                table: "Mfg_ProductionPlans");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseReceipts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");
        }
    }
}
