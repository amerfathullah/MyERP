using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemInspectionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InspectionRequiredBeforeDelivery",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InspectionRequiredBeforePurchase",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionRequiredBeforeDelivery",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "InspectionRequiredBeforePurchase",
                table: "Inv_Items");
        }
    }
}
