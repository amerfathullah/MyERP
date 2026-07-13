using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPRAmendmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Pur_PurchaseReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Pur_PurchaseReceipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Pur_PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Pur_PurchaseReceipts");
        }
    }
}
