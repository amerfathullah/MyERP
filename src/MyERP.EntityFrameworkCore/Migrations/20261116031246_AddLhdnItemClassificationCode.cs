using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLhdnItemClassificationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "Sal_SalesInvoiceItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassificationCode",
                table: "Pur_PurchaseInvoiceItems",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LhdnClassificationCode",
                table: "Inv_Items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ClassificationCode",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "LhdnClassificationCode",
                table: "Inv_Items");
        }
    }
}
