using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddChildEntityTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Tax_TransactionTaxRows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Sal_SalesOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Inv_StockEntryItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Acc_PaymentScheduleEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Acc_PaymentEntryReferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Acc_JournalEntryLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Tax_TransactionTaxRows");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Inv_StockEntryItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Acc_PaymentScheduleEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Acc_PaymentEntryReferences");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Acc_JournalEntryLines");
        }
    }
}
