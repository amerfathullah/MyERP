using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mfg_BOM",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BomNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalMaterialCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OperatingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_BOM", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProducedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaterialTransferred = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    WipWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FgWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Mfg_WorkOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_BOMItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Mfg_BOMItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_BOMItems_Mfg_BOM_BomId",
                        column: x => x.BomId,
                        principalTable: "Mfg_BOM",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_WorkOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TransferredQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_WorkOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_WorkOrderItems_Mfg_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "Mfg_WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BOM_TenantId_BomNumber",
                table: "Mfg_BOM",
                columns: new[] { "TenantId", "BomNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BOM_TenantId_ItemId_IsDefault",
                table: "Mfg_BOM",
                columns: new[] { "TenantId", "ItemId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_BOMItems_BomId",
                table: "Mfg_BOMItems",
                column: "BomId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkOrderItems_WorkOrderId",
                table: "Mfg_WorkOrderItems",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkOrders_TenantId_Status",
                table: "Mfg_WorkOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkOrders_TenantId_WorkOrderNumber",
                table: "Mfg_WorkOrders",
                columns: new[] { "TenantId", "WorkOrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_BOMItems");

            migrationBuilder.DropTable(
                name: "Mfg_WorkOrderItems");

            migrationBuilder.DropTable(
                name: "Mfg_BOM");

            migrationBuilder.DropTable(
                name: "Mfg_WorkOrders");
        }
    }
}
