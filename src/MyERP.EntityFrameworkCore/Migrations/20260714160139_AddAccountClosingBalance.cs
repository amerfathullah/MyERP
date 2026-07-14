using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountClosingBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_ClosingBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinanceBook = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    IsPeriodClosingEntry = table.Column<bool>(type: "boolean", nullable: false),
                    PeriodClosingVoucherId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_ClosingBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ClosingBalances_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ClosingBalances_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ClosingBalances_AccountId",
                table: "Acc_ClosingBalances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ClosingBalances_CompanyId",
                table: "Acc_ClosingBalances",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ClosingBalances_TenantId_CompanyId_ClosingDate",
                table: "Acc_ClosingBalances",
                columns: new[] { "TenantId", "CompanyId", "ClosingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ClosingBalances_TenantId_CompanyId_Period_AccountId_Cos~",
                table: "Acc_ClosingBalances",
                columns: new[] { "TenantId", "CompanyId", "Period", "AccountId", "CostCenterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_ClosingBalances");
        }
    }
}
