using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedgerEntryDependantSleVoucherDetailNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DependantSleVoucherDetailNo",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_DependantSleVoucherDetailNo",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "DependantSleVoucherDetailNo" },
                filter: "\"DependantSleVoucherDetailNo\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_DependantSleVoucherDetailNo",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "DependantSleVoucherDetailNo",
                table: "Inv_StockLedgerEntries");
        }
    }
}
