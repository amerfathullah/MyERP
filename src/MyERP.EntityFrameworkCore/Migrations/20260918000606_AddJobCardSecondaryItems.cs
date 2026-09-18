using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardSecondaryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mfg_JobCardSecondaryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StockQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockUom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SecondaryItemType = table.Column<int>(type: "integer", nullable: false),
                    BomSecondaryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Idx = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mfg_JobCardSecondaryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCardSecondaryItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCardSecondaryItems_Mfg_JobCards_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "Mfg_JobCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCardSecondaryItems_ItemId",
                table: "Mfg_JobCardSecondaryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCardSecondaryItems_JobCardId",
                table: "Mfg_JobCardSecondaryItems",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCardSecondaryItems_TenantId_JobCardId_ItemId",
                table: "Mfg_JobCardSecondaryItems",
                columns: new[] { "TenantId", "JobCardId", "ItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mfg_JobCardSecondaryItems");
        }
    }
}
