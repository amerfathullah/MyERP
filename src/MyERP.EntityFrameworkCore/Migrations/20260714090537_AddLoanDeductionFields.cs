using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDeductionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LoanDeduction",
                table: "Hr_PayrollEntryLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "LoanId",
                table: "Hr_PayrollEntryLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoanDeduction",
                table: "Hr_PayrollEntryLines");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "Hr_PayrollEntryLines");
        }
    }
}
