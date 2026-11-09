using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedgerEntryProjectIdAndProjectIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ProjectId",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockEntryItems_TenantId_ProjectId",
                table: "Inv_StockEntryItems",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentRequestId",
                table: "Acc_PaymentEntryReferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdvanceVoucherType",
                table: "Acc_PaymentEntryReferences",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdvanceVoucherId",
                table: "Acc_PaymentEntryReferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntryReferences_TenantId_PaymentRequestId",
                table: "Acc_PaymentEntryReferences",
                columns: new[] { "TenantId", "PaymentRequestId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Acc_PaymentEntryReferences_TenantId_PaymentRequestId",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropColumn(
                name: "AdvanceVoucherId",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropColumn(
                name: "AdvanceVoucherType",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropColumn(
                name: "PaymentRequestId",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockEntryItems_TenantId_ProjectId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ProjectId",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Inv_StockLedgerEntries");
        }
    }
}
