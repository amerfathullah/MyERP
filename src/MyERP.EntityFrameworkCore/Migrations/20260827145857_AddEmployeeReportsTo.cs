using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeReportsTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReturn",
                table: "Pur_SubcontractingReceipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstReceiptId",
                table: "Pur_SubcontractingReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportsToEmployeeId",
                table: "Hr_Employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShortYear",
                table: "Acc_FiscalYears",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReturn",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstReceiptId",
                table: "Pur_SubcontractingReceipts");

            migrationBuilder.DropColumn(
                name: "ReportsToEmployeeId",
                table: "Hr_Employees");

            migrationBuilder.DropColumn(
                name: "IsShortYear",
                table: "Acc_FiscalYears");
        }
    }
}
