using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAddressAndPIAmendment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillingAddressId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingAddressId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Pur_PurchaseInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "BillingAddressId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingAddressId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ShippingAddressId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BillingAddressId",
                table: "Pur_PurchaseInvoices");
        }
    }
}
