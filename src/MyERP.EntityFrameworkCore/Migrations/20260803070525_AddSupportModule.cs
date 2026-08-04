using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EInv_Consolidations");

            migrationBuilder.CreateTable(
                name: "Sup_ServiceLevelAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    HolidayListId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionTimeHours = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Sup_ServiceLevelAgreements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sup_ServiceLevelAgreements_TenantId_CompanyId_Name",
                table: "Sup_ServiceLevelAgreements",
                columns: new[] { "TenantId", "CompanyId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sup_ServiceLevelAgreements");

            migrationBuilder.CreateTable(
                name: "EInv_Consolidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConsolidatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    OriginalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInv_Consolidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EInv_Consolidations_Sal_SalesInvoices_ConsolidatedInvoiceId",
                        column: x => x.ConsolidatedInvoiceId,
                        principalTable: "Sal_SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EInv_Consolidations_Sal_SalesInvoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "Sal_SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_ConsolidatedInvoiceId",
                table: "EInv_Consolidations",
                column: "ConsolidatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_OriginalInvoiceId",
                table: "EInv_Consolidations",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EInv_Consolidations_TenantId_CompanyId_OriginalInvoiceId",
                table: "EInv_Consolidations",
                columns: new[] { "TenantId", "CompanyId", "OriginalInvoiceId" },
                unique: true);
        }
    }
}
