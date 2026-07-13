using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInterCompanyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RepresentsCompanyId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepresentsCompanyId",
                table: "Pur_Suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InterCompanyInvoiceId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepresentsCompanyId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "RepresentsCompanyId",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "InterCompanyInvoiceId",
                table: "Pur_PurchaseInvoices");
        }
    }
}
