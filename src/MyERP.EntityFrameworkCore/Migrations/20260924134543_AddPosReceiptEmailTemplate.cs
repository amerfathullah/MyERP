using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPosReceiptEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptEmailTemplateId",
                table: "Sal_PosProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_SubcontractingOrders",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceCostPerQty",
                table: "Pur_SubcontractingOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SetRateOfSubAssemblyItemBasedOnBom",
                table: "Mfg_BOMItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Inv_StockEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultSupplierId",
                table: "Inv_ItemGroups",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptEmailTemplateId",
                table: "Sal_PosProfiles");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Pur_SubcontractingOrders");

            migrationBuilder.DropColumn(
                name: "ServiceCostPerQty",
                table: "Pur_SubcontractingOrderItems");

            migrationBuilder.DropColumn(
                name: "SetRateOfSubAssemblyItemBasedOnBom",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "DefaultSupplierId",
                table: "Inv_ItemGroups");
        }
    }
}
