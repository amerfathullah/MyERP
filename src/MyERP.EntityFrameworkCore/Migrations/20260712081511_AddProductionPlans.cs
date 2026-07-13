using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mfg_ProductionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CombineItems = table.Column<bool>(type: "boolean", nullable: false),
                    IgnoreExistingOrderedQty = table.Column<bool>(type: "boolean", nullable: false),
                    ConsiderMinimumOrderQty = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeSafetyStock = table.Column<bool>(type: "boolean", nullable: false),
                    SkipAvailableSubAssemblyItem = table.Column<bool>(type: "boolean", nullable: false),
                    RawMaterialGroupWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Mfg_ProductionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_ProductionPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProducedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_ProductionPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_ProductionPlanItems_Mfg_ProductionPlans_ProductionPlanId",
                        column: x => x.ProductionPlanId,
                        principalTable: "Mfg_ProductionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_ProductionPlanMrItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AvailableQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PlannedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinOrderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcurementType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_ProductionPlanMrItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_ProductionPlanMrItems_Mfg_ProductionPlans_ProductionPla~",
                        column: x => x.ProductionPlanId,
                        principalTable: "Mfg_ProductionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlanItems_ProductionPlanId",
                table: "Mfg_ProductionPlanItems",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlanMrItems_ProductionPlanId",
                table: "Mfg_ProductionPlanMrItems",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlans_TenantId_PlanNumber",
                table: "Mfg_ProductionPlans",
                columns: new[] { "TenantId", "PlanNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_ProductionPlans_TenantId_Status",
                table: "Mfg_ProductionPlans",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_ProductionPlanItems");

            migrationBuilder.DropTable(
                name: "Mfg_ProductionPlanMrItems");

            migrationBuilder.DropTable(
                name: "Mfg_ProductionPlans");
        }
    }
}
