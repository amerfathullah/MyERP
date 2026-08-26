using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAndChequePrintTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_Banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SwiftNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ChequePrintTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChequeSize = table.Column<int>(type: "integer", nullable: false),
                    StartingPositionFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChequeWidth = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChequeHeight = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ScannedCheque = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAccountPayable = table.Column<bool>(type: "boolean", nullable: false),
                    AccPayDistFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccPayDistFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MessageToShow = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateDistFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DateDistFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PayerNameFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PayerNameFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInWordsFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInWordsFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInWordWidth = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInWordsLineSpacing = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInFiguresFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmtInFiguresFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccNoDistFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccNoDistFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SignatoryFromTopEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SignatoryFromLeftEdge = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HasPrintFormat = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_ChequePrintTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Banks_TenantId_BankName",
                table: "Acc_Banks",
                columns: new[] { "TenantId", "BankName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ChequePrintTemplates_TenantId_BankName",
                table: "Acc_ChequePrintTemplates",
                columns: new[] { "TenantId", "BankName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_Banks");

            migrationBuilder.DropTable(
                name: "Acc_ChequePrintTemplates");
        }
    }
}
