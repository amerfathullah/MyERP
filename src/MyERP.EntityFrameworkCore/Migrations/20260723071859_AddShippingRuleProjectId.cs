using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingRuleProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Sal_ShippingRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acc_FinancialReportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsStandard = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_FinancialReportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_FinancialReportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancialReportTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataSource = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ReferenceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CalculationFormula = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AccountCategoryFilter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomApiPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HideWhenEmpty = table.Column<bool>(type: "boolean", nullable: false),
                    IsBold = table.Column<bool>(type: "boolean", nullable: false),
                    IndentLevel = table.Column<int>(type: "integer", nullable: false),
                    SignMultiplier = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_FinancialReportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_FinancialReportRows_Acc_FinancialReportTemplates_Financ~",
                        column: x => x.FinancialReportTemplateId,
                        principalTable: "Acc_FinancialReportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_FinancialReportRows_FinancialReportTemplateId",
                table: "Acc_FinancialReportRows",
                column: "FinancialReportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_FinancialReportTemplates_TenantId_CompanyId_ReportType_~",
                table: "Acc_FinancialReportTemplates",
                columns: new[] { "TenantId", "CompanyId", "ReportType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_FinancialReportTemplates_TenantId_Name",
                table: "Acc_FinancialReportTemplates",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_FinancialReportRows");

            migrationBuilder.DropTable(
                name: "Acc_FinancialReportTemplates");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Sal_ShippingRules");
        }
    }
}
