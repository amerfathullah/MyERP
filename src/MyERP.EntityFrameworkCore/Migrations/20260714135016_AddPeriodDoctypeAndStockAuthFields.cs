using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodDoctypeAndStockAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockAuthRole",
                table: "AppCompanies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockFrozenUptoDays",
                table: "AppCompanies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClosedDocumentTypes",
                table: "Acc_AccountingPeriods",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockAuthRole",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "StockFrozenUptoDays",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ClosedDocumentTypes",
                table: "Acc_AccountingPeriods");
        }
    }
}
