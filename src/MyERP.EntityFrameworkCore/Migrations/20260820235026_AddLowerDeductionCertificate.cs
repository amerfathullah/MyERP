using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLowerDeductionCertificate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tax_LowerDeductionCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxWithholdingCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    CertificateLimit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidUpto = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_Tax_LowerDeductionCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_LowerDeductionCertificates_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tax_LowerDeductionCertificates_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tax_LowerDeductionCertificates_Tax_WithholdingCategories_Ta~",
                        column: x => x.TaxWithholdingCategoryId,
                        principalTable: "Tax_WithholdingCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_LowerDeductionCertificates_CompanyId",
                table: "Tax_LowerDeductionCertificates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_LowerDeductionCertificates_SupplierId",
                table: "Tax_LowerDeductionCertificates",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_LowerDeductionCertificates_TaxWithholdingCategoryId",
                table: "Tax_LowerDeductionCertificates",
                column: "TaxWithholdingCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_LowerDeductionCertificates_TenantId_SupplierId_TaxWithh~",
                table: "Tax_LowerDeductionCertificates",
                columns: new[] { "TenantId", "SupplierId", "TaxWithholdingCategoryId", "ValidFrom", "ValidUpto" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tax_LowerDeductionCertificates");
        }
    }
}
