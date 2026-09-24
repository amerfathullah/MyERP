using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerHoldAndBlanketOrderControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnHold",
                table: "Sal_Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "Sal_Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Sal_BlanketOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Sal_BlanketOrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnHold",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Sal_BlanketOrderItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Sal_BlanketOrderItems");
        }
    }
}
