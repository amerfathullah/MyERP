using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_LocationId",
                table: "Ast_Assets",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_Assets_Ast_Locations_LocationId",
                table: "Ast_Assets",
                column: "LocationId",
                principalTable: "Ast_Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ast_Assets_Ast_Locations_LocationId",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_LocationId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Ast_Assets");
        }
    }
}
