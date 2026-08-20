using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInvoiceHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HoldComment",
                table: "Pur_PurchaseInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnHold",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "Pur_PurchaseInvoices",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoldComment",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "OnHold",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Pur_PurchaseInvoices");
        }
    }
}
