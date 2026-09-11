using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemBatchExpiryAndShelfLife : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQty",
                table: "Sal_DeliveryNoteItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumberSeries",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreateNewBatch",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasExpiryDate",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RetainSample",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SampleQuantity",
                table: "Inv_Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeInDays",
                table: "Inv_Items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnedQty",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "BatchNumberSeries",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "CreateNewBatch",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "HasExpiryDate",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "RetainSample",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "SampleQuantity",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "ShelfLifeInDays",
                table: "Inv_Items");
        }
    }
}
