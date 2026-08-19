using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMovementLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceLocationId",
                table: "Ast_AssetMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetLocationId",
                table: "Ast_AssetMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceLocationId",
                table: "Ast_AssetMovementItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetLocationId",
                table: "Ast_AssetMovementItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_SourceLocationId",
                table: "Ast_AssetMovements",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_TargetLocationId",
                table: "Ast_AssetMovements",
                column: "TargetLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovementItems_SourceLocationId",
                table: "Ast_AssetMovementItems",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovementItems_TargetLocationId",
                table: "Ast_AssetMovementItems",
                column: "TargetLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetMovementItems_Ast_Locations_SourceLocationId",
                table: "Ast_AssetMovementItems",
                column: "SourceLocationId",
                principalTable: "Ast_Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetMovementItems_Ast_Locations_TargetLocationId",
                table: "Ast_AssetMovementItems",
                column: "TargetLocationId",
                principalTable: "Ast_Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetMovements_Ast_Locations_SourceLocationId",
                table: "Ast_AssetMovements",
                column: "SourceLocationId",
                principalTable: "Ast_Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ast_AssetMovements_Ast_Locations_TargetLocationId",
                table: "Ast_AssetMovements",
                column: "TargetLocationId",
                principalTable: "Ast_Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetMovementItems_Ast_Locations_SourceLocationId",
                table: "Ast_AssetMovementItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetMovementItems_Ast_Locations_TargetLocationId",
                table: "Ast_AssetMovementItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetMovements_Ast_Locations_SourceLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_Ast_AssetMovements_Ast_Locations_TargetLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovements_SourceLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovements_TargetLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovementItems_SourceLocationId",
                table: "Ast_AssetMovementItems");

            migrationBuilder.DropIndex(
                name: "IX_Ast_AssetMovementItems_TargetLocationId",
                table: "Ast_AssetMovementItems");

            migrationBuilder.DropColumn(
                name: "SourceLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "TargetLocationId",
                table: "Ast_AssetMovements");

            migrationBuilder.DropColumn(
                name: "SourceLocationId",
                table: "Ast_AssetMovementItems");

            migrationBuilder.DropColumn(
                name: "TargetLocationId",
                table: "Ast_AssetMovementItems");
        }
    }
}
