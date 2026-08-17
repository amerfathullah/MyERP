using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDowntimeEntryAndBomCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mfg_BomCreators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinishedGoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsPhantom = table.Column<bool>(type: "boolean", nullable: false),
                    RoutingId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RmCostAsPer = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RawMaterialCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorLog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Mfg_BomCreators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_BomCreators_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_BomCreators_Inv_Items_FinishedGoodItemId",
                        column: x => x.FinishedGoodItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_DowntimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DowntimeMinutes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StopReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Mfg_DowntimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_DowntimeEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_DowntimeEntries_Mfg_Workstations_WorkstationId",
                        column: x => x.WorkstationId,
                        principalTable: "Mfg_Workstations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_BomCreatorItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BomCreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FgItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsExpandable = table.Column<bool>(type: "boolean", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockUom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSubcontracted = table.Column<bool>(type: "boolean", nullable: false),
                    IsPhantomItem = table.Column<bool>(type: "boolean", nullable: false),
                    SourcedBySupplier = table.Column<bool>(type: "boolean", nullable: false),
                    Instruction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BomCreated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_BomCreatorItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_BomCreatorItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_BomCreatorItems_Mfg_BomCreators_BomCreatorId",
                        column: x => x.BomCreatorId,
                        principalTable: "Mfg_BomCreators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BomCreatorItems_BomCreatorId",
                table: "Mfg_BomCreatorItems",
                column: "BomCreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BomCreatorItems_ItemId",
                table: "Mfg_BomCreatorItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BomCreators_CompanyId",
                table: "Mfg_BomCreators",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BomCreators_FinishedGoodItemId",
                table: "Mfg_BomCreators",
                column: "FinishedGoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_DowntimeEntries_CompanyId",
                table: "Mfg_DowntimeEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_DowntimeEntries_TenantId_WorkstationId_FromTime",
                table: "Mfg_DowntimeEntries",
                columns: new[] { "TenantId", "WorkstationId", "FromTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_DowntimeEntries_WorkstationId",
                table: "Mfg_DowntimeEntries",
                column: "WorkstationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_BomCreatorItems");

            migrationBuilder.DropTable(
                name: "Mfg_DowntimeEntries");

            migrationBuilder.DropTable(
                name: "Mfg_BomCreators");
        }
    }
}
