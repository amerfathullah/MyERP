using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchSerialFieldsToSLE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "Sal_DeliveryNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerialNoId",
                table: "Sal_DeliveryNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerialNoId",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "SerialNoId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "SerialNoId",
                table: "Inv_StockLedgerEntries");
        }
    }
}
