using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPosOpeningAndPaymentTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_PaymentEntryTaxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeType = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseTaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IncludedInPaidAmount = table.Column<bool>(type: "boolean", nullable: false),
                    AddDeductTax = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountHead = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsExchangeGainLoss = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_PaymentEntryTaxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosOpeningEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OpeningTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PosClosingEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_PosOpeningEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosOpeningEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosOpeningPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PosOpeningEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeOfPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosOpeningPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosOpeningPayments_Sal_PosOpeningEntries_PosOpeningEntr~",
                        column: x => x.PosOpeningEntryId,
                        principalTable: "Sal_PosOpeningEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentEntryTaxes_PaymentEntryId",
                table: "Acc_PaymentEntryTaxes",
                column: "PaymentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosOpeningEntries_CompanyId",
                table: "Sal_PosOpeningEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosOpeningEntries_TenantId_CompanyId_PosProfileId_Status",
                table: "Sal_PosOpeningEntries",
                columns: new[] { "TenantId", "CompanyId", "PosProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosOpeningEntries_TenantId_UserId_Status",
                table: "Sal_PosOpeningEntries",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosOpeningPayments_PosOpeningEntryId",
                table: "Sal_PosOpeningPayments",
                column: "PosOpeningEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_PaymentEntryTaxes");

            migrationBuilder.DropTable(
                name: "Sal_PosOpeningPayments");

            migrationBuilder.DropTable(
                name: "Sal_PosOpeningEntries");
        }
    }
}
