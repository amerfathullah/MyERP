using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMfgConfigPricingRuleSRE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inv_StockReservationEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DeliveredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialAndBatchBundleId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inv_StockReservationEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_StockReservationEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockReservationEntries_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockReservationEntries_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkstationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreateJobCardBasedOnBatchSize = table.Column<bool>(type: "boolean", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    QualityInspectionTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCorrectiveOperation = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_Routings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_Routings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_Workstations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkstationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductionCapacity = table.Column<int>(type: "integer", nullable: false),
                    HourRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HolidayListId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Mfg_Workstations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_Workstations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicableFor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApplyOn = table.Column<int>(type: "integer", nullable: false),
                    ApplyOnId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplyOnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FreeItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    FreeItemQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaxQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidUpto = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyOnOtherItem = table.Column<bool>(type: "boolean", nullable: false),
                    OtherItemId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_PricingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_RoutingOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceId = table.Column<int>(type: "integer", nullable: false),
                    TimeInMins = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperatingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    HourRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsFixedTime = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_RoutingOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_RoutingOperations_Mfg_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Mfg_Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_RoutingOperations_Mfg_Routings_RoutingId",
                        column: x => x.RoutingId,
                        principalTable: "Mfg_Routings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_WorkstationCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatingComponent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperatingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_WorkstationCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_WorkstationCosts_Mfg_Workstations_WorkstationId",
                        column: x => x.WorkstationId,
                        principalTable: "Mfg_Workstations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_WorkstationWorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_WorkstationWorkingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_WorkstationWorkingHours_Mfg_Workstations_WorkstationId",
                        column: x => x.WorkstationId,
                        principalTable: "Mfg_Workstations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReservationEntries_CompanyId",
                table: "Inv_StockReservationEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReservationEntries_ItemId",
                table: "Inv_StockReservationEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReservationEntries_TenantId_ItemId_WarehouseId_Sta~",
                table: "Inv_StockReservationEntries",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReservationEntries_TenantId_VoucherType_VoucherId",
                table: "Inv_StockReservationEntries",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReservationEntries_WarehouseId",
                table: "Inv_StockReservationEntries",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Operations_TenantId_Name",
                table: "Mfg_Operations",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_RoutingOperations_OperationId",
                table: "Mfg_RoutingOperations",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_RoutingOperations_RoutingId",
                table: "Mfg_RoutingOperations",
                column: "RoutingId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Routings_TenantId_Name",
                table: "Mfg_Routings",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkstationCosts_WorkstationId",
                table: "Mfg_WorkstationCosts",
                column: "WorkstationId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Workstations_CompanyId",
                table: "Mfg_Workstations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Workstations_TenantId_CompanyId_Name",
                table: "Mfg_Workstations",
                columns: new[] { "TenantId", "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkstationWorkingHours_WorkstationId",
                table: "Mfg_WorkstationWorkingHours",
                column: "WorkstationId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PricingRules_TenantId_ApplyOn_ApplyOnId_Priority",
                table: "Sal_PricingRules",
                columns: new[] { "TenantId", "ApplyOn", "ApplyOnId", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_StockReservationEntries");

            migrationBuilder.DropTable(
                name: "Mfg_RoutingOperations");

            migrationBuilder.DropTable(
                name: "Mfg_WorkstationCosts");

            migrationBuilder.DropTable(
                name: "Mfg_WorkstationWorkingHours");

            migrationBuilder.DropTable(
                name: "Sal_PricingRules");

            migrationBuilder.DropTable(
                name: "Mfg_Operations");

            migrationBuilder.DropTable(
                name: "Mfg_Routings");

            migrationBuilder.DropTable(
                name: "Mfg_Workstations");
        }
    }
}
