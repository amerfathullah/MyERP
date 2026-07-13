using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillingAddressId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingAddressId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BillingAddressId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingAddressId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "ShippingAddressId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "BillingAddressId",
                table: "Pur_PurchaseOrders");
        }
    }
}
