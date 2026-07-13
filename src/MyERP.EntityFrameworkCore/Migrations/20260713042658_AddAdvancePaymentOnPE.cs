using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancePaymentOnPE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgainstOrderId",
                table: "Acc_PaymentEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgainstOrderType",
                table: "Acc_PaymentEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgainstOrderId",
                table: "Acc_PaymentEntries");

            migrationBuilder.DropColumn(
                name: "AgainstOrderType",
                table: "Acc_PaymentEntries");
        }
    }
}
