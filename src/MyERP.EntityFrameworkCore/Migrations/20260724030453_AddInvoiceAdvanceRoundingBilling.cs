using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAdvanceRoundingBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDate",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ItemId_WarehouseId_PostingD~",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRoundedTotal",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRoundingAdjustment",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DisableRoundedTotal",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundedTotal",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingAdjustment",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdvance",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteOffAccountId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WriteOffAmount",
                table: "Sal_SalesInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteOffCostCenterId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryNoteItemId",
                table: "Sal_SalesInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledQty",
                table: "Sal_DeliveryNoteItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledQty",
                table: "Pur_PurchaseReceiptItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRoundedTotal",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRoundingAdjustment",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "DisableRoundedTotal",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundedTotal",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundingAdjustment",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdvance",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteOffAccountId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WriteOffAmount",
                table: "Pur_PurchaseInvoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteOffCostCenterId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeferredExpenseAccountId",
                table: "Pur_PurchaseInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableDeferredExpense",
                table: "Pur_PurchaseInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceEndDate",
                table: "Pur_PurchaseInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceStartDate",
                table: "Pur_PurchaseInvoiceItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BomOperationId",
                table: "Mfg_WorkOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Mfg_WorkOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsAlternativeItem",
                table: "Mfg_WorkOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalItemId",
                table: "Mfg_WorkOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Mfg_WorkOrderItems",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossPercentage",
                table: "Mfg_BOM",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ScrapWarehouseId",
                table: "Mfg_BOM",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ValuationRate",
                table: "Inv_StockLedgerEntries",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AddColumn<string>(
                name: "FiscalYear",
                table: "Inv_StockLedgerEntries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBatchNo",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSerialNo",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "IncomingRate",
                table: "Inv_StockLedgerEntries",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdjustmentEntry",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OutgoingRate",
                table: "Inv_StockLedgerEntries",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostingDateTime",
                table: "Inv_StockLedgerEntries",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "PostingTime",
                table: "Inv_StockLedgerEntries",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "RecalculateRate",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SerialAndBatchBundleId",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Inv_StockLedgerEntries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockValueDifference",
                table: "Inv_StockLedgerEntries",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ViaLandedCostVoucher",
                table: "Inv_StockLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherDetailNo",
                table: "Inv_StockLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinishedItem",
                table: "Inv_StockEntryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossPercentage",
                table: "Inv_StockEntryItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryItemType",
                table: "Inv_StockEntryItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SetBasicRateManually",
                table: "Inv_StockEntryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceStockEntryDetailId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FgCompletedQty",
                table: "Inv_StockEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossPercentage",
                table: "Inv_StockEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessLossQty",
                table: "Inv_StockEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceStockEntryId",
                table: "Inv_StockEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableProformaInvoice",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Mfg_BOMSecondaryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SecondaryItemType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostAllocationPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProcessLossPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsLegacy = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_BOMSecondaryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_BOMSecondaryItems_Mfg_BOM_BomId",
                        column: x => x.BomId,
                        principalTable: "Mfg_BOM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ProformaInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProformaNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProformaDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    HideItemQty = table.Column<bool>(type: "boolean", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProformaPdfUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SentOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EmailedTo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_Sal_ProformaInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ProformaInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProformaInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_ProformaInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ProformaInvoiceItems_Sal_ProformaInvoices_ProformaInvoi~",
                        column: x => x.ProformaInvoiceId,
                        principalTable: "Sal_ProformaInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDateTime",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "CompanyId", "PostingDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ItemId_WarehouseId_PostingD~",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "PostingDateTime", "CreationTime" },
                filter: "\"IsCancelled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BOMSecondaryItems_BomId_ItemId",
                table: "Mfg_BOMSecondaryItems",
                columns: new[] { "BomId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProformaInvoiceItems_ProformaInvoiceId",
                table: "Sal_ProformaInvoiceItems",
                column: "ProformaInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProformaInvoices_TenantId_SalesOrderId_Status",
                table: "Sal_ProformaInvoices",
                columns: new[] { "TenantId", "SalesOrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_BOMSecondaryItems");

            migrationBuilder.DropTable(
                name: "Sal_ProformaInvoiceItems");

            migrationBuilder.DropTable(
                name: "Sal_ProformaInvoices");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDateTime",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ItemId_WarehouseId_PostingD~",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "BaseRoundedTotal",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BaseRoundingAdjustment",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DisableRoundedTotal",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "RoundedTotal",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "RoundingAdjustment",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAdvance",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffAccountId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffAmount",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffCostCenterId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteItemId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "BilledQty",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "BilledQty",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "BaseRoundedTotal",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BaseRoundingAdjustment",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DisableRoundedTotal",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "RoundedTotal",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "RoundingAdjustment",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAdvance",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffAccountId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffAmount",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "WriteOffCostCenterId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DeferredExpenseAccountId",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "EnableDeferredExpense",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceEndDate",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ServiceStartDate",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "BomOperationId",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "IsAlternativeItem",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "OriginalItemId",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "ProcessLossPercentage",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "ScrapWarehouseId",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "FiscalYear",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "HasBatchNo",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "HasSerialNo",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "IncomingRate",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "IsAdjustmentEntry",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "OutgoingRate",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "PostingDateTime",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "PostingTime",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "RecalculateRate",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "SerialAndBatchBundleId",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "StockValueDifference",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "ViaLandedCostVoucher",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "VoucherDetailNo",
                table: "Inv_StockLedgerEntries");

            migrationBuilder.DropColumn(
                name: "IsFinishedItem",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "ProcessLossPercentage",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "SecondaryItemType",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "SetBasicRateManually",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "SourceStockEntryDetailId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "FgCompletedQty",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "ProcessLossPercentage",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "ProcessLossQty",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "SourceStockEntryId",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "EnableProformaInvoice",
                table: "AppCompanies");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValuationRate",
                table: "Inv_StockLedgerEntries",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_CompanyId_PostingDate",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockLedgerEntries_TenantId_ItemId_WarehouseId_PostingD~",
                table: "Inv_StockLedgerEntries",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "PostingDate" });
        }
    }
}
