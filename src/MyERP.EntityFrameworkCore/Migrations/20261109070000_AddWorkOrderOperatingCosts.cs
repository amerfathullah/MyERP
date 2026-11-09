using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderOperatingCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PlannedOperatingCost",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualOperatingCost",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CorrectiveOperationCost",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalOperatingCost",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalOperatingCost",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "CorrectiveOperationCost",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ActualOperatingCost",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "PlannedOperatingCost",
                table: "Mfg_WorkOrders");
        }
    }
}
