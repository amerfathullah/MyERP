using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPETaxesAssetDetailLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPointsEarned",
                table: "Sal_SalesInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPointsRedeemed",
                table: "Sal_SalesInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LoyaltyProgramId",
                table: "Sal_SalesInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyRedemptionAmount",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Ast_DepreciationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepreciationMethod = table.Column<int>(type: "integer", nullable: false),
                    TotalNumberOfDepreciations = table.Column<int>(type: "integer", nullable: false),
                    FrequencyOfDepreciation = table.Column<int>(type: "integer", nullable: false),
                    DepreciationStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpectedValueAfterUsefulLife = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetPurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpeningAccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpeningNumberOfBookedDepreciations = table.Column<int>(type: "integer", nullable: false),
                    ValueAfterDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_DepreciationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_DepreciationDetails_Ast_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Ast_Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_DepreciationDetails_AssetId_FinanceBookId",
                table: "Ast_DepreciationDetails",
                columns: new[] { "AssetId", "FinanceBookId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Acc_PaymentEntryTaxes_Acc_PaymentEntries_PaymentEntryId",
                table: "Acc_PaymentEntryTaxes",
                column: "PaymentEntryId",
                principalTable: "Acc_PaymentEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Acc_PaymentEntryTaxes_Acc_PaymentEntries_PaymentEntryId",
                table: "Acc_PaymentEntryTaxes");

            migrationBuilder.DropTable(
                name: "Ast_DepreciationDetails");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsEarned",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LoyaltyPointsRedeemed",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LoyaltyProgramId",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "LoyaltyRedemptionAmount",
                table: "Sal_SalesInvoices");
        }
    }
}
