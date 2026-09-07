using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDebitNoteAndReturnRefundToInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Tax_TransactionTaxRows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Sal_SalesOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OrderType",
                table: "Sal_SalesOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SkipDeliveryNote",
                table: "Sal_SalesOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Sal_SalesOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQty",
                table: "Sal_SalesOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "QuotationItemId",
                table: "Sal_SalesOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedQty",
                table: "Sal_SalesOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsolidated",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDebitNote",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPos",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnRefund",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PosProfileId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssetId",
                table: "Sal_SalesInvoiceItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFixedAsset",
                table: "Sal_SalesInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Sal_ProductBundles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HideUnavailableItems",
                table: "Sal_PosProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Sal_PosProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Sal_DeliveryNoteItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LeadId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OpportunityId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProspectId",
                table: "Sal_Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Sal_BlanketOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Sal_BlanketOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseRate",
                table: "Sal_BlanketOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                table: "Pur_Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQty",
                table: "Pur_SupplierQuotationItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Pur_SubcontractingReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Pur_SubcontractingReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceExpenseAccountId",
                table: "Pur_SubcontractingReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Pur_SubcontractingOrderSuppliedItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Pur_SubcontractingOrderSuppliedItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Pur_PurchaseReceiptItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DeliveredBySupplier",
                table: "Pur_PurchaseOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Pur_PurchaseOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierQuotationItemId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDebitNote",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnRefund",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DeliveredBySupplier",
                table: "Pur_PurchaseInvoiceItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LandedCostVoucherAmount",
                table: "Pur_PurchaseInvoiceItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_MaterialRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "Pur_MaterialRequestItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CollectProgress",
                table: "Prj_Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Prj_Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Prj_Projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Prj_Projects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalConsumedMaterialCost",
                table: "Prj_Projects",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "FromWipWarehouse",
                table: "Mfg_WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdditionalItem",
                table: "Mfg_WorkOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherDetailReference",
                table: "Mfg_WorkOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BatchSplit",
                table: "Mfg_RoutingOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerPiece",
                table: "Mfg_RoutingOperations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BatchSplit",
                table: "Mfg_JobCards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerPiece",
                table: "Mfg_JobCards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValuationType",
                table: "Mfg_BOMSecondaryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "BatchSplit",
                table: "Mfg_BOMOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "QualityInspectionRequired",
                table: "Mfg_BOMOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerPiece",
                table: "Mfg_BOMOperations",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkstationTypeId",
                table: "Mfg_BOMOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DoNotExplode",
                table: "Mfg_BOMItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FromVoucherDetailId",
                table: "Inv_StockReservationEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FromVoucherId",
                table: "Inv_StockReservationEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromVoucherType",
                table: "Inv_StockReservationEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentSerialAndBatchBundleId",
                table: "Inv_StockReconciliationItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerialAndBatchBundleId",
                table: "Inv_StockReconciliationItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCost",
                table: "Inv_StockEntryItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Inv_StockEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpening",
                table: "Inv_StockEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Inv_StockEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdditionalCosts",
                table: "Inv_StockEntries",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Inv_SerialAndBatchBundles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInventoryAccountId",
                table: "Inv_Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultInventoryAccountId",
                table: "Inv_ItemGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Inv_Batches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedValueAfterUsefulLife",
                table: "Ast_Assets",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompositeAsset",
                table: "Ast_Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompositeComponent",
                table: "Ast_Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "StockEntryId",
                table: "Ast_AssetRepairs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncreaseInAssetLife",
                table: "Ast_AssetDepreciationDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefaultSalesContact",
                table: "AppCompanies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableItemWiseInventoryAccount",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionBudgetApproverRole",
                table: "AppCompanies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationTakesEffectOn",
                table: "AppCompanies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceExpenseAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SubmitErrJournals",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePaymentAmount",
                table: "Acc_PaymentScheduleEntries",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconcileEffectOn",
                table: "Acc_PaymentEntryReferences",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Acc_PaymentEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActionIfAccumulatedMonthlyExceededOnCumulativeExpense",
                table: "Acc_Budgets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ActionIfAnnualExceededOnCumulativeExpense",
                table: "Acc_Budgets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ApplicableOnCumulativeExpense",
                table: "Acc_Budgets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MonthlyDistributionId",
                table: "Acc_Budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MaintainSameInternalTransactionRate",
                table: "Acc_AccountsSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaintainSameRateAction",
                table: "Acc_AccountsSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoleToOverrideStopAction",
                table: "Acc_AccountsSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Acc_AccountingPeriods",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Tax_TransactionTaxRows");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "SkipDeliveryNote",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "OrderedQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "QuotationItemId",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "RequestedQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "IsConsolidated",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsDebitNote",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsPos",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsReturnRefund",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PosProfileId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "IsFixedAsset",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Sal_ProductBundles");

            migrationBuilder.DropColumn(
                name: "HideUnavailableItems",
                table: "Sal_PosProfiles");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Sal_PosProfiles");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "OpportunityId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "ProspectId",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Sal_BlanketOrders");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Sal_BlanketOrders");

            migrationBuilder.DropColumn(
                name: "BaseRate",
                table: "Sal_BlanketOrderItems");

            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "OrderedQty",
                table: "Pur_SupplierQuotationItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "ServiceExpenseAccountId",
                table: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Pur_SubcontractingOrderSuppliedItems");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Pur_SubcontractingOrderSuppliedItems");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "DeliveredBySupplier",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SupplierQuotationItemId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "IsDebitNote",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "IsReturnRefund",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "DeliveredBySupplier",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "LandedCostVoucherAmount",
                table: "Pur_PurchaseInvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_MaterialRequests");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "CollectProgress",
                table: "Prj_Projects");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Prj_Projects");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Prj_Projects");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Prj_Projects");

            migrationBuilder.DropColumn(
                name: "TotalConsumedMaterialCost",
                table: "Prj_Projects");

            migrationBuilder.DropColumn(
                name: "FromWipWarehouse",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsAdditionalItem",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "VoucherDetailReference",
                table: "Mfg_WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "BatchSplit",
                table: "Mfg_RoutingOperations");

            migrationBuilder.DropColumn(
                name: "WeightPerPiece",
                table: "Mfg_RoutingOperations");

            migrationBuilder.DropColumn(
                name: "BatchSplit",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "WeightPerPiece",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "ValuationType",
                table: "Mfg_BOMSecondaryItems");

            migrationBuilder.DropColumn(
                name: "BatchSplit",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "QualityInspectionRequired",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "WeightPerPiece",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "WorkstationTypeId",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "DoNotExplode",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "FromVoucherDetailId",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "FromVoucherId",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "FromVoucherType",
                table: "Inv_StockReservationEntries");

            migrationBuilder.DropColumn(
                name: "CurrentSerialAndBatchBundleId",
                table: "Inv_StockReconciliationItems");

            migrationBuilder.DropColumn(
                name: "SerialAndBatchBundleId",
                table: "Inv_StockReconciliationItems");

            migrationBuilder.DropColumn(
                name: "AdditionalCost",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "IsOpening",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "TotalAdditionalCosts",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Inv_SerialAndBatchBundles");

            migrationBuilder.DropColumn(
                name: "DefaultInventoryAccountId",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "DefaultInventoryAccountId",
                table: "Inv_ItemGroups");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Inv_Batches");

            migrationBuilder.DropColumn(
                name: "ExpectedValueAfterUsefulLife",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "IsCompositeAsset",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "IsCompositeComponent",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "StockEntryId",
                table: "Ast_AssetRepairs");

            migrationBuilder.DropColumn(
                name: "IncreaseInAssetLife",
                table: "Ast_AssetDepreciationDetails");

            migrationBuilder.DropColumn(
                name: "DefaultSalesContact",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "EnableItemWiseInventoryAccount",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ExceptionBudgetApproverRole",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ReconciliationTakesEffectOn",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ServiceExpenseAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "SubmitErrJournals",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "BasePaymentAmount",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "ReconcileEffectOn",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "ActionIfAccumulatedMonthlyExceededOnCumulativeExpense",
                table: "Acc_Budgets");

            migrationBuilder.DropColumn(
                name: "ActionIfAnnualExceededOnCumulativeExpense",
                table: "Acc_Budgets");

            migrationBuilder.DropColumn(
                name: "ApplicableOnCumulativeExpense",
                table: "Acc_Budgets");

            migrationBuilder.DropColumn(
                name: "MonthlyDistributionId",
                table: "Acc_Budgets");

            migrationBuilder.DropColumn(
                name: "MaintainSameInternalTransactionRate",
                table: "Acc_AccountsSettings");

            migrationBuilder.DropColumn(
                name: "MaintainSameRateAction",
                table: "Acc_AccountsSettings");

            migrationBuilder.DropColumn(
                name: "RoleToOverrideStopAction",
                table: "Acc_AccountsSettings");

            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Acc_AccountingPeriods");
        }
    }
}
