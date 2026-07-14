using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyingControllerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSubcontracted",
                table: "Pur_PurchaseReceipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FromWarehouseId",
                table: "Pur_PurchaseReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Pur_PurchaseReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Acc_BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Deposit",
                table: "Acc_BankTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExcludedFee",
                table: "Acc_BankTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncludedFee",
                table: "Acc_BankTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Withdrawal",
                table: "Acc_BankTransactions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSubcontracted",
                table: "Pur_PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "FromWarehouseId",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseReceiptId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Acc_BankTransactions");

            migrationBuilder.DropColumn(
                name: "Deposit",
                table: "Acc_BankTransactions");

            migrationBuilder.DropColumn(
                name: "ExcludedFee",
                table: "Acc_BankTransactions");

            migrationBuilder.DropColumn(
                name: "IncludedFee",
                table: "Acc_BankTransactions");

            migrationBuilder.DropColumn(
                name: "Withdrawal",
                table: "Acc_BankTransactions");
        }
    }
}
