using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderItemSalesOrderLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderItemId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossQty",
                table: "Mfg_ProductionPlanItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LostOn",
                table: "CRM_Opportunities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseOrderItems_SalesOrderItemId",
                table: "Pur_PurchaseOrderItems",
                column: "SalesOrderItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseOrderItems_SalesOrderItemId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ProcessLossQty",
                table: "Mfg_ProductionPlanItems");

            migrationBuilder.DropColumn(
                name: "LostOn",
                table: "CRM_Opportunities");
        }
    }
}
