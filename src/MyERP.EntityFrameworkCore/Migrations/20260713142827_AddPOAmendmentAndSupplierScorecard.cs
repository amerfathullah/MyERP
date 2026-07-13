using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPOAmendmentAndSupplierScorecard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PreventPurchaseOrders",
                table: "Pur_Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreventRfqs",
                table: "Pur_Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Pur_PurchaseOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreventPurchaseOrders",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "PreventRfqs",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Pur_PurchaseOrders");
        }
    }
}
