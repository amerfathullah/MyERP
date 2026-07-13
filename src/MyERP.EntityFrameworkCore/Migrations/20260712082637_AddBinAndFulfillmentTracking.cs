using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBinAndFulfillmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BilledQty",
                table: "Sal_SalesOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveredQty",
                table: "Sal_SalesOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Sal_SalesOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BilledQty",
                table: "Pur_PurchaseOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                table: "Pur_PurchaseOrderItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Pur_PurchaseOrderItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Inv_Bins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PlannedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IndentedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservedQtyForProduction = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservedQtyForSubContract = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReservedQtyForProductionPlan = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ValuationRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_Bins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_Bins_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_Bins_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Bins_ItemId",
                table: "Inv_Bins",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Bins_TenantId_ItemId_WarehouseId",
                table: "Inv_Bins",
                columns: new[] { "TenantId", "ItemId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Bins_WarehouseId",
                table: "Inv_Bins",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_Bins");

            migrationBuilder.DropColumn(
                name: "BilledQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "DeliveredQty",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "BilledQty",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Pur_PurchaseOrderItems");
        }
    }
}
