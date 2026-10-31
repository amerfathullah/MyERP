using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBomFgBasedOperatingCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPhantomBom",
                table: "Mfg_BOM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FgBasedOperatingCost",
                table: "Mfg_BOM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OperatingCostPerBomQuantity",
                table: "Mfg_BOM",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperatingCostPerBomQuantity",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "FgBasedOperatingCost",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "IsPhantomBom",
                table: "Mfg_BOM");
        }
    }
}
