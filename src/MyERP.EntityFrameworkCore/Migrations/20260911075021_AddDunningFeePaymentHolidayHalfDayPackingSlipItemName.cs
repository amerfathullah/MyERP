using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningFeePaymentHolidayHalfDayPackingSlipItemName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "Sal_PackingSlipItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidDunningAmount",
                table: "Sal_Dunnings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsHalfDay",
                table: "Hr_Holidays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DunningId",
                table: "Acc_PaymentEntryTaxes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntryTaxes_DunningId",
                table: "Acc_PaymentEntryTaxes",
                column: "DunningId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Acc_PaymentEntryTaxes_DunningId",
                table: "Acc_PaymentEntryTaxes");

            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "Sal_PackingSlipItems");

            migrationBuilder.DropColumn(
                name: "PaidDunningAmount",
                table: "Sal_Dunnings");

            migrationBuilder.DropColumn(
                name: "IsHalfDay",
                table: "Hr_Holidays");

            migrationBuilder.DropColumn(
                name: "DunningId",
                table: "Acc_PaymentEntryTaxes");
        }
    }
}
