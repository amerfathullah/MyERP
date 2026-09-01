using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges20260901 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "Sal_Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationDate",
                table: "Sal_Subscriptions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactPersonId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingContactPersonId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQty",
                table: "Sal_SalesOrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SkipDelivery",
                table: "Sal_SalesOrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactPersonId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingContactPersonId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContactPersonId",
                table: "Sal_DeliveryNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingContactPersonId",
                table: "Sal_DeliveryNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderItemId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderItemId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipTransfer",
                table: "Mfg_WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TrackSemiFinishedGoods",
                table: "Mfg_WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReserveStock",
                table: "Mfg_ProductionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PendingQty",
                table: "Mfg_JobCards",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FinishedGoodItemId",
                table: "Mfg_BOMOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalFinishedGood",
                table: "Mfg_BOMOperations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBalanceItem",
                table: "Mfg_BOMItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Percentage",
                table: "Mfg_BOMItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SetQtyBasedOnPercentage",
                table: "Mfg_BOM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StockUom",
                table: "Inv_StockReconciliationItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFgConversion",
                table: "Inv_StockEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "JobCardId",
                table: "Inv_StockEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightPerPiece",
                table: "Inv_StockEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQty",
                table: "Inv_PickListItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentBatchId",
                table: "Inv_Batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowCreditNoteWithoutOriginalInvoice",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "BankChargesAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeGainAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExchangeLossAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsASubscription",
                table: "Acc_PaymentRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "Acc_PaymentRequests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "Sal_Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancellationDate",
                table: "Sal_Subscriptions");

            migrationBuilder.DropColumn(
                name: "ContactPersonId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "ShippingContactPersonId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "ReturnedQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "SkipDelivery",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "ContactPersonId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ShippingContactPersonId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ContactPersonId",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "ShippingContactPersonId",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "SalesOrderItemId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "SkipTransfer",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "TrackSemiFinishedGoods",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ReserveStock",
                table: "Mfg_ProductionPlans");

            migrationBuilder.DropColumn(
                name: "PendingQty",
                table: "Mfg_JobCards");

            migrationBuilder.DropColumn(
                name: "FinishedGoodItemId",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "IsFinalFinishedGood",
                table: "Mfg_BOMOperations");

            migrationBuilder.DropColumn(
                name: "IsBalanceItem",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "Mfg_BOMItems");

            migrationBuilder.DropColumn(
                name: "SetQtyBasedOnPercentage",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "StockUom",
                table: "Inv_StockReconciliationItems");

            migrationBuilder.DropColumn(
                name: "IsFgConversion",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "JobCardId",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "WeightPerPiece",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "DeliveredQty",
                table: "Inv_PickListItems");

            migrationBuilder.DropColumn(
                name: "ParentBatchId",
                table: "Inv_Batches");

            migrationBuilder.DropColumn(
                name: "AllowCreditNoteWithoutOriginalInvoice",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "BankChargesAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ExchangeGainAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ExchangeLossAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "IsASubscription",
                table: "Acc_PaymentRequests");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Acc_PaymentRequests");
        }
    }
}
