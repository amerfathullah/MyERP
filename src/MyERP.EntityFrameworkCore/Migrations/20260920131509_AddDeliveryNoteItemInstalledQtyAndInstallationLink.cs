using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNoteItemInstalledQtyAndInstallationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostAllocationPercentage",
                table: "Mfg_BOM");

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryNoteItemId",
                table: "Sal_InstallationNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InstalledQty",
                table: "Sal_DeliveryNoteItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_InstallationNoteItems_DeliveryNoteItemId",
                table: "Sal_InstallationNoteItems",
                column: "DeliveryNoteItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sal_InstallationNoteItems_Sal_DeliveryNoteItems_DeliveryNot~",
                table: "Sal_InstallationNoteItems",
                column: "DeliveryNoteItemId",
                principalTable: "Sal_DeliveryNoteItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sal_InstallationNoteItems_Sal_DeliveryNoteItems_DeliveryNot~",
                table: "Sal_InstallationNoteItems");

            migrationBuilder.DropIndex(
                name: "IX_Sal_InstallationNoteItems_DeliveryNoteItemId",
                table: "Sal_InstallationNoteItems");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteItemId",
                table: "Sal_InstallationNoteItems");

            migrationBuilder.DropColumn(
                name: "InstalledQty",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.AddColumn<decimal>(
                name: "CostAllocationPercentage",
                table: "Mfg_BOM",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
