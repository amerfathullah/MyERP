using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintFormatBuilderTypeAndData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormatData",
                table: "Set_PrintFormats",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FormatType",
                table: "Set_PrintFormats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableLhdnInvoice",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormatData",
                table: "Set_PrintFormats");

            migrationBuilder.DropColumn(
                name: "FormatType",
                table: "Set_PrintFormats");

            migrationBuilder.DropColumn(
                name: "EnableLhdnInvoice",
                table: "AppCompanies");
        }
    }
}
