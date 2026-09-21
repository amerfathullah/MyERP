using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceRequirementExemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowPurchaseInvoiceWithoutPurchaseOrder",
                table: "Pur_Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPurchaseInvoiceWithoutPurchaseReceipt",
                table: "Pur_Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowPurchaseInvoiceWithoutPurchaseOrder",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "AllowPurchaseInvoiceWithoutPurchaseReceipt",
                table: "Pur_Suppliers");
        }
    }
}
