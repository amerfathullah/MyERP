using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderReserveStockAndItemReservedQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Campaign",
                table: "Sal_PricingRules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReserveStock",
                table: "Mfg_WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StockReservedQty",
                table: "Mfg_WorkOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SerialAndBatchBundleId",
                table: "Inv_PickListItems",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Campaign",
                table: "Sal_PricingRules");

            migrationBuilder.DropColumn(
                name: "ReserveStock",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "StockReservedQty",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "SerialAndBatchBundleId",
                table: "Inv_PickListItems");
        }
    }
}
