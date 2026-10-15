using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTaskTaskTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubmitToLhdn",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateBilledAmountInSalesOrder",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReturnQtyFromRejectedWarehouse",
                table: "Pur_PurchaseReceiptItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubmitToLhdn",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskTypeId",
                table: "Prj_Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskTypeId",
                table: "Prj_ProjectTemplateTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowAlternativeItem",
                table: "Mfg_WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalItemId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNoSeries",
                table: "Inv_Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseSerialNoWiseValuation",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "CRM_Leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "AppCompanies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Tasks_TaskTypeId",
                table: "Prj_Tasks",
                column: "TaskTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ProjectTemplateTasks_TaskTypeId",
                table: "Prj_ProjectTemplateTasks",
                column: "TaskTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prj_Tasks_TaskTypeId",
                table: "Prj_Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Prj_ProjectTemplateTasks_TaskTypeId",
                table: "Prj_ProjectTemplateTasks");

            migrationBuilder.DropColumn(
                name: "IsSubmitToLhdn",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "UpdateBilledAmountInSalesOrder",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ReturnQtyFromRejectedWarehouse",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "IsSubmitToLhdn",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "Prj_Tasks");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "Prj_ProjectTemplateTasks");

            migrationBuilder.DropColumn(
                name: "AllowAlternativeItem",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "OriginalItemId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "SerialNoSeries",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "UseSerialNoWiseValuation",
                table: "Inv_Items");

            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "CRM_Leads");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "AppCompanies");
        }
    }
}
