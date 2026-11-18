using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationItemBlanketOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BlanketOrderId",
                table: "Sal_QuotationItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCodeUrl",
                table: "Pur_PurchaseInvoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlanketOrderId",
                table: "Sal_QuotationItems");

            migrationBuilder.DropColumn(
                name: "QrCodeUrl",
                table: "Pur_PurchaseInvoices");
        }
    }
}
