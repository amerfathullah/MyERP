using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodClosingStockValueDifference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MaterialRequestId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaterialRequestItemId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FgBatchNo",
                table: "Inv_SerialAndBatchEntries",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FgSerialNo",
                table: "Inv_SerialAndBatchEntries",
                type: "character varying(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManualInspection",
                table: "Inv_QualityInspectionReadings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StockValueDifference",
                table: "Acc_PeriodClosingVouchers",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkOrders_TenantId_MaterialRequestId",
                table: "Mfg_WorkOrders",
                columns: new[] { "TenantId", "MaterialRequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mfg_WorkOrders_TenantId_MaterialRequestId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "MaterialRequestId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "MaterialRequestItemId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "FgBatchNo",
                table: "Inv_SerialAndBatchEntries");

            migrationBuilder.DropColumn(
                name: "FgSerialNo",
                table: "Inv_SerialAndBatchEntries");

            migrationBuilder.DropColumn(
                name: "ManualInspection",
                table: "Inv_QualityInspectionReadings");

            migrationBuilder.DropColumn(
                name: "StockValueDifference",
                table: "Acc_PeriodClosingVouchers");
        }
    }
}
