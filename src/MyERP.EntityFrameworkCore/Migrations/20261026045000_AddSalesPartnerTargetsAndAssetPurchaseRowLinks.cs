using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPartnerTargetsAndAssetPurchaseRowLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sal_SalesPartnerTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DistributionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_SalesPartnerTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_SalesPartnerTargets_Sal_SalesPartners_SalesPartnerId",
                        column: x => x.SalesPartnerId,
                        principalTable: "Sal_SalesPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesPartnerTargets_SalesPartnerId_FiscalYearId_ItemGroupId",
                table: "Sal_SalesPartnerTargets",
                columns: new[] { "SalesPartnerId", "FiscalYearId", "ItemGroupId" });

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseReceiptItemId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseInvoiceItemId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_PurchaseReceiptItemId",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "PurchaseReceiptItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_PurchaseInvoiceItemId",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "PurchaseInvoiceItemId" });

            migrationBuilder.AddColumn<Guid>(
                name: "SalesPartnerId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "Sal_SalesOrders",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCommission",
                table: "Sal_SalesOrders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesOrders_TenantId_SalesPartnerId",
                table: "Sal_SalesOrders",
                columns: new[] { "TenantId", "SalesPartnerId" });

            migrationBuilder.AddColumn<Guid>(
                name: "SalesPartnerId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "Sal_SalesInvoices",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCommission",
                table: "Sal_SalesInvoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SalesInvoices_TenantId_SalesPartnerId",
                table: "Sal_SalesInvoices",
                columns: new[] { "TenantId", "SalesPartnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_SalesPartnerTargets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_PurchaseReceiptItemId",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_PurchaseInvoiceItemId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseReceiptItemId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceItemId",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Sal_SalesOrders_TenantId_SalesPartnerId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "SalesPartnerId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "TotalCommission",
                table: "Sal_SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_Sal_SalesInvoices_TenantId_SalesPartnerId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SalesPartnerId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "TotalCommission",
                table: "Sal_SalesInvoices");
        }
    }
}
