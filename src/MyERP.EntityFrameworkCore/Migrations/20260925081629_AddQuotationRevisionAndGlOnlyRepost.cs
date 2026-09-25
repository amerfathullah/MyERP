using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationRevisionAndGlOnlyRepost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Sal_Quotations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevisionIndex",
                table: "Sal_Quotations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOfId",
                table: "Sal_Quotations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Pur_PurchaseOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RepostOnlyAccountingLedgers",
                table: "Inv_RepostItemValuations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_IsActive",
                table: "Sal_Quotations",
                columns: new[] { "TenantId", "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_RevisionOfId",
                table: "Sal_Quotations",
                columns: new[] { "TenantId", "CompanyId", "RevisionOfId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_RepostItemValuations_TenantId_CompanyId_VoucherId_Vouch~",
                table: "Inv_RepostItemValuations",
                columns: new[] { "TenantId", "CompanyId", "VoucherId", "VoucherType", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_IsActive",
                table: "Sal_Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Sal_Quotations_TenantId_CompanyId_RevisionOfId",
                table: "Sal_Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Inv_RepostItemValuations_TenantId_CompanyId_VoucherId_Vouch~",
                table: "Inv_RepostItemValuations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "RevisionIndex",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "RevisionOfId",
                table: "Sal_Quotations");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Pur_PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RepostOnlyAccountingLedgers",
                table: "Inv_RepostItemValuations");
        }
    }
}
