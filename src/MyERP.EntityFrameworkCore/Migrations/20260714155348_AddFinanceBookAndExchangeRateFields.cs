using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceBookAndExchangeRateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Sal_DeliveryNotes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseReceipts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Inv_StockEntries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Inv_StockEntries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceBookId",
                table: "Ast_DepreciationSchedule",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acc_FinanceBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_Acc_FinanceBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_FinanceBooks_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_FinanceBooks_CompanyId",
                table: "Acc_FinanceBooks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_FinanceBooks_TenantId_CompanyId_Name",
                table: "Acc_FinanceBooks",
                columns: new[] { "TenantId", "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_FinanceBooks");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Sal_DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Pur_PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Inv_StockEntries");

            migrationBuilder.DropColumn(
                name: "FinanceBookId",
                table: "Ast_DepreciationSchedule");
        }
    }
}
