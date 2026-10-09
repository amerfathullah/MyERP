using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardToMaterialRequestAndJobCardTransferredQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JobCardId",
                table: "Pur_MaterialRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JobCardItemId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransferredQty",
                table: "Mfg_JobCards",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequests_JobCardId",
                table: "Pur_MaterialRequests",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequestItems_JobCardItemId",
                table: "Pur_MaterialRequestItems",
                column: "JobCardItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pur_MaterialRequests_JobCardId",
                table: "Pur_MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_Pur_MaterialRequestItems_JobCardItemId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "JobCardId",
                table: "Pur_MaterialRequests");

            migrationBuilder.DropColumn(
                name: "JobCardItemId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "TransferredQty",
                table: "Mfg_JobCards");
        }
    }
}
