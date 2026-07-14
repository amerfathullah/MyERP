using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxWithholdingEntryAndSupplierCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxWithholdingCategory",
                table: "Pur_Suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tax_WithholdingEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WithholdingRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WithheldAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HasLDC = table.Column<bool>(type: "boolean", nullable: false),
                    LdcRate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Tax_WithholdingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_WithholdingEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingEntries_CompanyId",
                table: "Tax_WithholdingEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingEntries_TenantId_PartyId_PostingDate",
                table: "Tax_WithholdingEntries",
                columns: new[] { "TenantId", "PartyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tax_WithholdingEntries_TenantId_VoucherType_VoucherId",
                table: "Tax_WithholdingEntries",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tax_WithholdingEntries");

            migrationBuilder.DropColumn(
                name: "TaxWithholdingCategory",
                table: "Pur_Suppliers");
        }
    }
}
