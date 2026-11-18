using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNoteItemSettledQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalDeliveryNoteItemId",
                table: "Sal_DeliveryNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledQty",
                table: "Sal_DeliveryNoteItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalDeliveryNoteItemId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "SettledQty",
                table: "Sal_DeliveryNoteItems");
        }
    }
}
