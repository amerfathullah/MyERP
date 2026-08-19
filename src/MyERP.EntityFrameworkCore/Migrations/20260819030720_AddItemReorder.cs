using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemReorder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inv_ItemReorders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseReorderLevel = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseReorderQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaterialRequestType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_ItemReorders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemReorders_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_ItemReorders_Inv_Warehouses_WarehouseGroupId",
                        column: x => x.WarehouseGroupId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inv_ItemReorders_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemReorders_ItemId_WarehouseId",
                table: "Inv_ItemReorders",
                columns: new[] { "ItemId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemReorders_WarehouseGroupId",
                table: "Inv_ItemReorders",
                column: "WarehouseGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemReorders_WarehouseId",
                table: "Inv_ItemReorders",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_ItemReorders");
        }
    }
}
