using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryItemValuationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BomSecondaryItemId",
                table: "Pur_SubcontractingReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostAllocationPercentage",
                table: "Pur_SubcontractingReceiptItems",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryItemType",
                table: "Pur_SubcontractingReceiptItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationType",
                table: "Pur_SubcontractingReceiptItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FgCostAllocationPercentage",
                table: "Mfg_BOM",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryItemType",
                table: "Inv_StockEntryItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BomSecondaryItemId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationType",
                table: "Inv_StockEntryItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceiptItems_BomSecondaryItemId",
                table: "Pur_SubcontractingReceiptItems",
                column: "BomSecondaryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockEntryItems_BomSecondaryItemId",
                table: "Inv_StockEntryItems",
                column: "BomSecondaryItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingReceiptItems_BomSecondaryItemId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockEntryItems_BomSecondaryItemId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "BomSecondaryItemId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "CostAllocationPercentage",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "SecondaryItemType",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "ValuationType",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "FgCostAllocationPercentage",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "BomSecondaryItemId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "ValuationType",
                table: "Inv_StockEntryItems");

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryItemType",
                table: "Inv_StockEntryItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
