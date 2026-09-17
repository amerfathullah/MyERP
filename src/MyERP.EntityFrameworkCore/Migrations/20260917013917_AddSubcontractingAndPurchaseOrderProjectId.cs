using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractingAndPurchaseOrderProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_SubcontractingReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_SubcontractingReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_SubcontractingOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_SubcontractingOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceipts_ProjectId",
                table: "Pur_SubcontractingReceipts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceiptItems_ProjectId",
                table: "Pur_SubcontractingReceiptItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrders_ProjectId",
                table: "Pur_SubcontractingOrders",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrderItems_ProjectId",
                table: "Pur_SubcontractingOrderItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseOrderItems_ProjectId",
                table: "Pur_PurchaseOrderItems",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingReceipts_ProjectId",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingReceiptItems_ProjectId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingOrders_ProjectId",
                table: "Pur_SubcontractingOrders");

            migrationBuilder.DropIndex(
                name: "IX_Pur_SubcontractingOrderItems_ProjectId",
                table: "Pur_SubcontractingOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseOrderItems_ProjectId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_SubcontractingOrders");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_SubcontractingOrderItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_PurchaseOrderItems");
        }
    }
}
