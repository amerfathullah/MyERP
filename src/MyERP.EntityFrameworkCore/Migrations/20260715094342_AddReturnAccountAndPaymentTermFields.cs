using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnAccountAndPaymentTermFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DebitToAccountId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CreditToAccountId",
                table: "Pur_PurchaseInvoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "Acc_PaymentEntryReferences",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebitToAccountId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CreditToAccountId",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Acc_PaymentEntryReferences");
        }
    }
}
