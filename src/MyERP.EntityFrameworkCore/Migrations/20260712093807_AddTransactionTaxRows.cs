using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionTaxRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tax_TransactionTaxRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChargeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReferenceRowIndex = table.Column<int>(type: "integer", nullable: true),
                    TaxCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IncludedInPrintRate = table.Column<bool>(type: "boolean", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmountAfterDiscount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseTaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tax_TransactionTaxRows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_TransactionTaxRows_ParentType_ParentId",
                table: "Tax_TransactionTaxRows",
                columns: new[] { "ParentType", "ParentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tax_TransactionTaxRows");
        }
    }
}
