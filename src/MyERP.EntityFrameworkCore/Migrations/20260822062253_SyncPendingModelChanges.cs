using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SplitFromIssueId",
                table: "Sup_Issues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FollowCalendarMonths",
                table: "Sal_Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrepaid",
                table: "Sal_Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PartyItemCode",
                table: "Sal_BlanketOrderItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                table: "Pur_PurchaseReceiptItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQty",
                table: "Pur_PurchaseReceiptItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackSemiFinishedGoods",
                table: "Mfg_BOM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TransferMaterialAgainst",
                table: "Mfg_BOM",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "WithOperations",
                table: "Mfg_BOM",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdditionalTransferEntry",
                table: "Inv_StockEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscount",
                table: "Inv_Items",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarrantyClaimId",
                table: "Ast_MaintenanceVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetQuantity",
                table: "Ast_Assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SplitFromAssetId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultWipAccountId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsYourCompanyAddress",
                table: "AppAddresses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxCategory",
                table: "AppAddresses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SplitFromIssueId",
                table: "Sup_Issues");

            migrationBuilder.DropColumn(
                name: "FollowCalendarMonths",
                table: "Sal_Subscriptions");

            migrationBuilder.DropColumn(
                name: "IsPrepaid",
                table: "Sal_Subscriptions");

            migrationBuilder.DropColumn(
                name: "PartyItemCode",
                table: "Sal_BlanketOrderItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "RejectedQty",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "RejectedWarehouseId",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "TrackSemiFinishedGoods",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "TransferMaterialAgainst",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "WithOperations",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "IsAdditionalTransferEntry",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "MaxDiscount",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "WarrantyClaimId",
                table: "Ast_MaintenanceVisits");

            migrationBuilder.DropColumn(
                name: "AssetQuantity",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "SplitFromAssetId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "DefaultWipAccountId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "IsYourCompanyAddress",
                table: "AppAddresses");

            migrationBuilder.DropColumn(
                name: "TaxCategory",
                table: "AppAddresses");
        }
    }
}
