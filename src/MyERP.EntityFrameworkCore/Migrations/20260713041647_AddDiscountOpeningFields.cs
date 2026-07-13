using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountOpeningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalDiscountPercentage",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpening",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalDiscountPercentage",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpening",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalDiscountPercentage",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsOpening",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AdditionalDiscountPercentage",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "IsOpening",
                table: "Pur_PurchaseInvoices");
        }
    }
}
