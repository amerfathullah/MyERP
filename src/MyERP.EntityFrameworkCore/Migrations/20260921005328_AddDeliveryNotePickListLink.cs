using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryNotePickListLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PickListId",
                table: "Sal_DeliveryNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickListItemId",
                table: "Sal_DeliveryNoteItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_PickListId",
                table: "Sal_DeliveryNotes",
                column: "PickListId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNoteItems_PickListItemId",
                table: "Sal_DeliveryNoteItems",
                column: "PickListItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sal_DeliveryNoteItems_Inv_PickListItems_PickListItemId",
                table: "Sal_DeliveryNoteItems",
                column: "PickListItemId",
                principalTable: "Inv_PickListItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Sal_DeliveryNotes_Inv_PickLists_PickListId",
                table: "Sal_DeliveryNotes",
                column: "PickListId",
                principalTable: "Inv_PickLists",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sal_DeliveryNoteItems_Inv_PickListItems_PickListItemId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Sal_DeliveryNotes_Inv_PickLists_PickListId",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_Sal_DeliveryNotes_PickListId",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_Sal_DeliveryNoteItems_PickListItemId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "PickListId",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "PickListItemId",
                table: "Sal_DeliveryNoteItems");
        }
    }
}
