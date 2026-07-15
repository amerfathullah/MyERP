using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetRepairCapitalizationAndBomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BackflushBasedOn",
                table: "Mfg_BOM",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Ast_Capitalizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapitalizationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TargetAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetAssetName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TotalCapitalizedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Ast_Capitalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_Capitalizations_Ast_Assets_TargetAssetId",
                        column: x => x.TargetAssetId,
                        principalTable: "Ast_Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_Repairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FailureDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RepairCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CapitalizeRepairCost = table.Column<bool>(type: "boolean", nullable: false),
                    IncreaseInAssetLife = table.Column<int>(type: "integer", nullable: false),
                    StockItemConsumedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Ast_Repairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_Repairs_Ast_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Ast_Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_CapitalizationAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AssetCapitalizationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_CapitalizationAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_CapitalizationAssets_Ast_Capitalizations_AssetCapitaliz~",
                        column: x => x.AssetCapitalizationId,
                        principalTable: "Ast_Capitalizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ast_CapitalizationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCapitalizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetCapitalizationId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_CapitalizationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliza~",
                        column: x => x.AssetCapitalizationId,
                        principalTable: "Ast_Capitalizations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ast_CapitalizationItems_Ast_Capitalizations_AssetCapitaliz~1",
                        column: x => x.AssetCapitalizationId1,
                        principalTable: "Ast_Capitalizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_CapitalizationAssets_AssetCapitalizationId",
                table: "Ast_CapitalizationAssets",
                column: "AssetCapitalizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_CapitalizationItems_AssetCapitalizationId",
                table: "Ast_CapitalizationItems",
                column: "AssetCapitalizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_CapitalizationItems_AssetCapitalizationId1",
                table: "Ast_CapitalizationItems",
                column: "AssetCapitalizationId1");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Capitalizations_TargetAssetId",
                table: "Ast_Capitalizations",
                column: "TargetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Capitalizations_TenantId_CompanyId_Status",
                table: "Ast_Capitalizations",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Repairs_AssetId",
                table: "Ast_Repairs",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Repairs_TenantId_AssetId_Status",
                table: "Ast_Repairs",
                columns: new[] { "TenantId", "AssetId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_CapitalizationAssets");

            migrationBuilder.DropTable(
                name: "Ast_CapitalizationItems");

            migrationBuilder.DropTable(
                name: "Ast_Repairs");

            migrationBuilder.DropTable(
                name: "Ast_Capitalizations");

            migrationBuilder.AlterColumn<string>(
                name: "BackflushBasedOn",
                table: "Mfg_BOM",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
