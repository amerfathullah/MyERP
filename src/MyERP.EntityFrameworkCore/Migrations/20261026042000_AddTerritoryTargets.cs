using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTerritoryTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Core_TerritoryTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerritoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ItemGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    DistributionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_TerritoryTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_TerritoryTargets_Core_Territories_TerritoryId",
                        column: x => x.TerritoryId,
                        principalTable: "Core_Territories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_TerritoryTargets_TerritoryId_FiscalYearId_ItemGroupId",
                table: "Core_TerritoryTargets",
                columns: new[] { "TerritoryId", "FiscalYearId", "ItemGroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Core_TerritoryTargets");
        }
    }
}
