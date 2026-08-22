using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListDefaultsToPartiesAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Sal_Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPriceListId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPriceListId",
                table: "Pur_Suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "DefaultPriceListId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "DefaultPriceListId",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                table: "Pur_PurchaseInvoices");
        }
    }
}
