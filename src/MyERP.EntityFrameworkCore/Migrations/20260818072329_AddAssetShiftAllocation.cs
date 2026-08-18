using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetShiftAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseDepreciationAmount",
                table: "Ast_DepreciationScheduleEntries",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftFactorId",
                table: "Ast_DepreciationScheduleEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Ast_AssetShiftAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllocationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinanceBookId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetShiftAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetShiftFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShiftName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Ast_AssetShiftFactors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetShiftAllocationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetShiftAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftFactorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Ast_AssetShiftAllocationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetShiftAllocationLines_Ast_AssetShiftAllocations_Ass~",
                        column: x => x.AssetShiftAllocationId,
                        principalTable: "Ast_AssetShiftAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetShiftAllocationLines_AssetShiftAllocationId_Schedu~",
                table: "Ast_AssetShiftAllocationLines",
                columns: new[] { "AssetShiftAllocationId", "ScheduleEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetShiftAllocations_TenantId_AssetId",
                table: "Ast_AssetShiftAllocations",
                columns: new[] { "TenantId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetShiftFactors_TenantId_ShiftName",
                table: "Ast_AssetShiftFactors",
                columns: new[] { "TenantId", "ShiftName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_AssetShiftAllocationLines");

            migrationBuilder.DropTable(
                name: "Ast_AssetShiftFactors");

            migrationBuilder.DropTable(
                name: "Ast_AssetShiftAllocations");

            migrationBuilder.DropColumn(
                name: "BaseDepreciationAmount",
                table: "Ast_DepreciationScheduleEntries");

            migrationBuilder.DropColumn(
                name: "ShiftFactorId",
                table: "Ast_DepreciationScheduleEntries");
        }
    }
}
