using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceReturnAndPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Sal_SalesInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermsTemplateId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Pur_PurchaseInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermsTemplateId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeOfPaymentId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentTermsTemplateId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentTermsTemplateId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ModeOfPaymentId",
                table: "Acc_PaymentEntries");
        }
    }
}
