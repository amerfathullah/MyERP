using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetOpeningDepreciationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DepreciationStartDate",
                table: "Ast_Assets",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpeningNumberOfBookedDepreciations",
                table: "Ast_Assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepreciationStartDate",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "OpeningNumberOfBookedDepreciations",
                table: "Ast_Assets");
        }
    }
}
