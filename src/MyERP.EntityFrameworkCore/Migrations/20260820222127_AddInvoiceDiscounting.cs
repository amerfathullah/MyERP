using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDiscounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceStopDate",
                table: "Sal_SalesInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasUnitPriceItems",
                table: "Sal_Quotations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Sal_QuotationItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Sal_QuotationItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasUnitPriceItems",
                table: "Pur_SupplierQuotations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Pur_SupplierQuotationItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Pur_SupplierQuotationItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdvancePaymentStatus",
                table: "Pur_PurchaseOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSubcontracted",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FromWarehouseId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceStopDate",
                table: "Pur_PurchaseInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GrantCommission",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "AppDocumentSeries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<bool>(
                name: "AllowUomWithConversionRateDefinedInItem",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInTransitWarehouseId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWarehouseForSalesReturnId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Acc_PaymentEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Acc_JournalEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Acc_JournalEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Acc_InvoiceDiscountings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LoanStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LoanPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    LoanEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BankCharges = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ShortTermLoanAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankChargesAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountsReceivableCreditAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountsReceivableDiscountedAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountsReceivableUnpaidAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SanctionJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisbursementJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_InvoiceDiscountings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_InvoiceDiscountings_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_InvoiceDiscountingInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceDiscountingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_InvoiceDiscountingInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_InvoiceDiscountingInvoices_Acc_InvoiceDiscountings_Invo~",
                        column: x => x.InvoiceDiscountingId,
                        principalTable: "Acc_InvoiceDiscountings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_InvoiceDiscountingInvoices_InvoiceDiscountingId",
                table: "Acc_InvoiceDiscountingInvoices",
                column: "InvoiceDiscountingId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_InvoiceDiscountingInvoices_SalesInvoiceId",
                table: "Acc_InvoiceDiscountingInvoices",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_InvoiceDiscountings_CompanyId",
                table: "Acc_InvoiceDiscountings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_InvoiceDiscountings_TenantId_CompanyId_Status",
                table: "Acc_InvoiceDiscountings",
                columns: new[] { "TenantId", "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_InvoiceDiscountingInvoices");

            migrationBuilder.DropTable(
                name: "Acc_InvoiceDiscountings");

            migrationBuilder.DropColumn(
                name: "ServiceStopDate",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "HasUnitPriceItems",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Sal_QuotationItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Sal_QuotationItems");

            migrationBuilder.DropColumn(
                name: "HasUnitPriceItems",
                table: "Pur_SupplierQuotations");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Pur_SupplierQuotationItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Pur_SupplierQuotationItems");

            migrationBuilder.DropColumn(
                name: "AdvancePaymentStatus",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IsSubcontracted",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "FromWarehouseId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceStopDate",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "GrantCommission",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "AllowUomWithConversionRateDefinedInItem",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultInTransitWarehouseId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultWarehouseForSalesReturnId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Acc_JournalEntries");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Acc_JournalEntries");

            migrationBuilder.AlterColumn<string>(
                name: "Prefix",
                table: "AppDocumentSeries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
