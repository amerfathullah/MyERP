using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentEntryInternalTransferBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TermsAndConditionsId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "Sal_SalesInvoices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermsAndConditionsId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermsAndConditionsId",
                table: "Sal_Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentScheduleId",
                table: "Sal_DunningOverduePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermsAndConditionsId",
                table: "Pur_PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Terms",
                table: "Pur_PurchaseInvoices",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermsAndConditionsId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalOperatingCost",
                table: "Mfg_WorkOrders",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RecalculateValuationRate",
                table: "Inv_RepostItemValuations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBuyingTermsId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultSellingTermsId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidFromBankAccountId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidToBankAccountId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntries_PaidFromBankAccountId",
                table: "Acc_PaymentEntries",
                column: "PaidFromBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntries_PaidToBankAccountId",
                table: "Acc_PaymentEntries",
                column: "PaidToBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Acc_PaymentEntries_Acc_BankAccounts_PaidFromBankAccountId",
                table: "Acc_PaymentEntries",
                column: "PaidFromBankAccountId",
                principalTable: "Acc_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Acc_PaymentEntries_Acc_BankAccounts_PaidToBankAccountId",
                table: "Acc_PaymentEntries",
                column: "PaidToBankAccountId",
                principalTable: "Acc_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acc_PaymentEntries_Acc_BankAccounts_PaidFromBankAccountId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Acc_PaymentEntries_Acc_BankAccounts_PaidToBankAccountId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropIndex(
                name: "IX_Acc_PaymentEntries_PaidFromBankAccountId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropIndex(
                name: "IX_Acc_PaymentEntries_PaidToBankAccountId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "TermsAndConditionsId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TermsAndConditionsId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TermsAndConditionsId",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "PaymentScheduleId",
                table: "Sal_DunningOverduePayments");

            migrationBuilder.DropColumn(
                name: "TermsAndConditionsId",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Terms",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "TermsAndConditionsId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "AdditionalOperatingCost",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "RecalculateValuationRate",
                table: "Inv_RepostItemValuations");

            migrationBuilder.DropColumn(
                name: "DefaultBuyingTermsId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "DefaultSellingTermsId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "PaidFromBankAccountId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "PaidToBankAccountId",
                table: "Acc_PaymentEntries");
        }
    }
}
