using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationWorkstationTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkstationTypeId",
                table: "Mfg_Operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Operations_WorkstationTypeId",
                table: "Mfg_Operations",
                column: "WorkstationTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mfg_Operations_Mfg_WorkstationTypes_WorkstationTypeId",
                table: "Mfg_Operations",
                column: "WorkstationTypeId",
                principalTable: "Mfg_WorkstationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mfg_Operations_Mfg_WorkstationTypes_WorkstationTypeId",
                table: "Mfg_Operations");

            migrationBuilder.DropIndex(
                name: "IX_Mfg_Operations_WorkstationTypeId",
                table: "Mfg_Operations");

            migrationBuilder.DropColumn(
                name: "WorkstationTypeId",
                table: "Mfg_Operations");
        }
    }
}
