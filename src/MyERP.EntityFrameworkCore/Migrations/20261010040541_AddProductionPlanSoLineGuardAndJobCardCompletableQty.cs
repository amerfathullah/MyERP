using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPlanSoLineGuardAndJobCardCompletableQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OverproductionPercentageForSalesOrder",
                table: "Mfg_Settings",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsProductBundleItem",
                table: "Mfg_ProductionPlanItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderItemId",
                table: "Mfg_ProductionPlanItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlanItems_SalesOrderId_SalesOrderItemId",
                table: "Mfg_ProductionPlanItems",
                columns: new[] { "SalesOrderId", "SalesOrderItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mfg_ProductionPlanItems_SalesOrderId_SalesOrderItemId",
                table: "Mfg_ProductionPlanItems");

            migrationBuilder.DropColumn(
                name: "OverproductionPercentageForSalesOrder",
                table: "Mfg_Settings");

            migrationBuilder.DropColumn(
                name: "IsProductBundleItem",
                table: "Mfg_ProductionPlanItems");

            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "Mfg_ProductionPlanItems");
        }
    }
}
