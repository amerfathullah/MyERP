using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponCodeToSoAndSi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Sal_SalesOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Sal_SalesInvoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Sal_SalesInvoices");
        }
    }
}
