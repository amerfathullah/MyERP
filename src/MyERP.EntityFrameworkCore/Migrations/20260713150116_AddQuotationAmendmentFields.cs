using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationAmendmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Sal_Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Sal_Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Sal_Quotations");
        }
    }
}
