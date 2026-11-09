using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPartnerWebsiteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowInWebsite",
                table: "Sal_SalesPartners",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Sal_SalesPartners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Introduction",
                table: "Sal_SalesPartners",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowInWebsite",
                table: "Sal_SalesPartners");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Sal_SalesPartners");

            migrationBuilder.DropColumn(
                name: "Introduction",
                table: "Sal_SalesPartners");
        }
    }
}
