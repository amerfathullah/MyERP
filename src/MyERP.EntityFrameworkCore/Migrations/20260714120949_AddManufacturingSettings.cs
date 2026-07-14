using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mfg_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverproductionPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    BackflushRawMaterialsBasedOn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaterialConsumption = table.Column<bool>(type: "boolean", nullable: false),
                    TransferExtraMaterialsPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MinsBetweenOperations = table.Column<int>(type: "integer", nullable: false),
                    CapacityPlanningForDays = table.Column<int>(type: "integer", nullable: false),
                    MakeSerialNoBatchFromWorkOrder = table.Column<bool>(type: "boolean", nullable: false),
                    UpdateBomCostsAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    AllowOvertime = table.Column<bool>(type: "boolean", nullable: false),
                    AllowProductionOnHolidays = table.Column<bool>(type: "boolean", nullable: false),
                    DisableCapacityPlanning = table.Column<bool>(type: "boolean", nullable: false),
                    JobCardExcessTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    EnforceTimeLogs = table.Column<bool>(type: "boolean", nullable: false),
                    AddCorrectiveOpCostInFGValuation = table.Column<bool>(type: "boolean", nullable: false),
                    ValidateComponentsQuantitiesPerBom = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_Settings_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Settings_CompanyId",
                table: "Mfg_Settings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Settings_TenantId_CompanyId",
                table: "Mfg_Settings",
                columns: new[] { "TenantId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_Settings");
        }
    }
}
