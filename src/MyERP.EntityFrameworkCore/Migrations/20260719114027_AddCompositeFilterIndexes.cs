using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeFilterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesOrders_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_SalesOrders",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_Quotations",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_DeliveryNotes",
                columns: new[] { "TenantId", "CompanyId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceipts_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseReceipts",
                columns: new[] { "TenantId", "CompanyId", "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseInvoices_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseInvoices",
                columns: new[] { "TenantId", "CompanyId", "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockEntries_TenantId_CompanyId_EntryType_Status",
                table: "Inv_StockEntries",
                columns: new[] { "TenantId", "CompanyId", "EntryType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntries_TenantId_CompanyId_PostingDate",
                table: "Acc_PaymentEntries",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sal_SalesOrders_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Sal_DeliveryNotes_TenantId_CompanyId_CustomerId_Status",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseReceipts_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Pur_PurchaseInvoices_TenantId_CompanyId_SupplierId_Status",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockEntries_TenantId_CompanyId_EntryType_Status",
                table: "Inv_StockEntries");

            migrationBuilder.DropIndex(
                name: "IX_Acc_PaymentEntries_TenantId_CompanyId_PostingDate",
                table: "Acc_PaymentEntries");
        }
    }
}
