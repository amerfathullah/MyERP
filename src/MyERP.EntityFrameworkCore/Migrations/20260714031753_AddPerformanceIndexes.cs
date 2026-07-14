using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesInvoices_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_SalesInvoices",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseOrders_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseOrders",
                columns: new[] { "TenantId", "CompanyId", "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDate",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_VoucherType_VoucherId",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntries_TenantId_CompanyId_PostingDate",
                table: "Acc_JournalEntries",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntries_TenantId_CompanyId_Status",
                table: "Acc_JournalEntries",
                columns: new[] { "TenantId", "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sal_SalesInvoices_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseOrders_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDate",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_VoucherType_VoucherId",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Acc_JournalEntries_TenantId_CompanyId_PostingDate",
                table: "Acc_JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_Acc_JournalEntries_TenantId_CompanyId_Status",
                table: "Acc_JournalEntries");
        }
    }
}
