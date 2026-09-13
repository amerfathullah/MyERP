using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReconciliationAllowZeroValuationRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostAllocationPercentage",
                table: "Mfg_BOM",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "AllowZeroValuationRate",
                table: "Inv_StockReconciliationItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostAllocationPercentage",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "AllowZeroValuationRate",
                table: "Inv_StockReconciliationItems");
        }
    }
}
