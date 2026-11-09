using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderAdvancePaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdvancePaymentStatus",
                table: "Sal_SalesOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Not Requested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvancePaymentStatus",
                table: "Sal_SalesOrders");
        }
    }
}
