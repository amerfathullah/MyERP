using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemReorderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderLevel",
                table: "Inv_Items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReorderQty",
                table: "Inv_Items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SafetyStock",
                table: "Inv_Items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultWarehouseId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "ReorderQty",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "SafetyStock",
                table: "Inv_Items");
        }
    }
}
