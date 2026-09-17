using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPickListItemStockReservedQtyAndBundleLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "DeliveredQty",
                table: "Inv_PickListItems",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductBundleItemId",
                table: "Inv_PickListItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockReservedQty",
                table: "Inv_PickListItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductBundleItemId",
                table: "Inv_PickListItems");

            migrationBuilder.DropColumn(
                name: "StockReservedQty",
                table: "Inv_PickListItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "DeliveredQty",
                table: "Inv_PickListItems",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");
        }
    }
}
