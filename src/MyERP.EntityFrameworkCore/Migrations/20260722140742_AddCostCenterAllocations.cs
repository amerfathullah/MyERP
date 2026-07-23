using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCostCenterAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_CostCenterAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MainCostCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_Acc_CostCenterAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_CostCenterAllocations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_CostCenterAllocationEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildCostCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_CostCenterAllocationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_CostCenterAllocationEntries_Acc_CostCenterAllocations_C~",
                        column: x => x.CostCenterAllocationId,
                        principalTable: "Acc_CostCenterAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CostCenterAllocationEntries_CostCenterAllocationId",
                table: "Acc_CostCenterAllocationEntries",
                column: "CostCenterAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CostCenterAllocationEntries_TenantId_CostCenterAllocati~",
                table: "Acc_CostCenterAllocationEntries",
                columns: new[] { "TenantId", "CostCenterAllocationId", "ChildCostCenterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CostCenterAllocations_CompanyId",
                table: "Acc_CostCenterAllocations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CostCenterAllocations_TenantId_CompanyId_MainCostCenter~",
                table: "Acc_CostCenterAllocations",
                columns: new[] { "TenantId", "CompanyId", "MainCostCenterId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_CostCenterAllocationEntries");

            migrationBuilder.DropTable(
                name: "Acc_CostCenterAllocations");
        }
    }
}
