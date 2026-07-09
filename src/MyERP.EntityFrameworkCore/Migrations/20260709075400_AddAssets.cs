using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ast_Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustodianEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PurchaseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AdditionalCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CalculateDepreciation = table.Column<bool>(type: "boolean", nullable: false),
                    DepreciationMethod = table.Column<int>(type: "integer", nullable: false),
                    UsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    DepreciationRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    FrequencyMonths = table.Column<int>(type: "integer", nullable: false),
                    AvailableForUseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpeningAccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ValueAfterDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsFullyDepreciated = table.Column<bool>(type: "boolean", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisposalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_Ast_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDepreciable = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultDepreciationMethod = table.Column<int>(type: "integer", nullable: false),
                    DefaultUsefulLifeMonths = table.Column<int>(type: "integer", nullable: false),
                    DefaultDepreciationRate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    AssetAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepreciationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccumulatedDepreciationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_DepreciationSchedule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DepreciationAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsBooked = table.Column<bool>(type: "boolean", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_DepreciationSchedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_DepreciationSchedule_Ast_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Ast_Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_AssetNumber",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "AssetNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_Status",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Categories_TenantId_CategoryName",
                table: "Ast_Categories",
                columns: new[] { "TenantId", "CategoryName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_DepreciationSchedule_AssetId_ScheduleDate",
                table: "Ast_DepreciationSchedule",
                columns: new[] { "AssetId", "ScheduleDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_Categories");

            migrationBuilder.DropTable(
                name: "Ast_DepreciationSchedule");

            migrationBuilder.DropTable(
                name: "Ast_Assets");
        }
    }
}
