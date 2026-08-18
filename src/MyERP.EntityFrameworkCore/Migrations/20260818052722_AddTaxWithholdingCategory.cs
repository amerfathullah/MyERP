using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxWithholdingCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tax_WithholdingCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TaxDeductionBasis = table.Column<int>(type: "integer", nullable: false),
                    RoundOffTaxAmount = table.Column<bool>(type: "boolean", nullable: false),
                    TaxOnExcessAmount = table.Column<bool>(type: "boolean", nullable: false),
                    DisableCumulativeThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    DisableTransactionThreshold = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Tax_WithholdingCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tax_WithholdingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxWithholdingCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_Tax_WithholdingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_WithholdingAccounts_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tax_WithholdingAccounts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tax_WithholdingAccounts_Tax_WithholdingCategories_TaxWithho~",
                        column: x => x.TaxWithholdingCategoryId,
                        principalTable: "Tax_WithholdingCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tax_WithholdingRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxWithholdingCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    SingleThreshold = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CumulativeThreshold = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Tax_WithholdingRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_WithholdingRates_Tax_WithholdingCategories_TaxWithholdi~",
                        column: x => x.TaxWithholdingCategoryId,
                        principalTable: "Tax_WithholdingCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingAccounts_AccountId",
                table: "Tax_WithholdingAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingAccounts_CompanyId",
                table: "Tax_WithholdingAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingAccounts_TaxWithholdingCategoryId_CompanyId",
                table: "Tax_WithholdingAccounts",
                columns: new[] { "TaxWithholdingCategoryId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingCategories_TenantId_CategoryName",
                table: "Tax_WithholdingCategories",
                columns: new[] { "TenantId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingRates_TaxWithholdingCategoryId",
                table: "Tax_WithholdingRates",
                column: "TaxWithholdingCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tax_WithholdingAccounts");

            migrationBuilder.DropTable(
                name: "Tax_WithholdingRates");

            migrationBuilder.DropTable(
                name: "Tax_WithholdingCategories");
        }
    }
}
