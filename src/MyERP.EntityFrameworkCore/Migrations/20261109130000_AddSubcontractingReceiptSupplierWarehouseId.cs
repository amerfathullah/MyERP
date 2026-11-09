using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractingReceiptSupplierWarehouseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierWarehouseId",
                table: "Pur_SubcontractingReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceipts_SupplierWarehouseId",
                table: "Pur_SubcontractingReceipts",
                column: "SupplierWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pur_SubcontractingReceipts_Inv_Warehouses_SupplierWarehouse~",
                table: "Pur_SubcontractingReceipts",
                column: "SupplierWarehouseId",
                principalTable: "Inv_Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pur_SubcontractingReceipts_Inv_Warehouses_SupplierWarehouse~",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingReceipts_SupplierWarehouseId",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierWarehouseId",
                table: "Pur_SubcontractingReceipts");
        }
    }
}
