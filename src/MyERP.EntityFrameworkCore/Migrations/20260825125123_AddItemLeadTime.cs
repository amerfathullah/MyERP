using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemLeadTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inv_ItemLeadTimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftTimeInHours = table.Column<int>(type: "integer", nullable: false),
                    NoOfWorkstations = table.Column<int>(type: "integer", nullable: false),
                    NoOfShifts = table.Column<int>(type: "integer", nullable: false),
                    TotalWorkstationTime = table.Column<int>(type: "integer", nullable: false),
                    ManufacturingTimeInMins = table.Column<int>(type: "integer", nullable: false),
                    DailyYield = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NoOfUnitsProduced = table.Column<int>(type: "integer", nullable: false),
                    CapacityPerDay = table.Column<int>(type: "integer", nullable: false),
                    PurchaseTimeDays = table.Column<int>(type: "integer", nullable: false),
                    BufferTimeDays = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemLeadTimes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemLeadTimeSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemLeadTimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseTimeDays = table.Column<int>(type: "integer", nullable: false),
                    BufferTimeDays = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemLeadTimeSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemLeadTimeSuppliers_Inv_ItemLeadTimes_ItemLeadTimeId",
                        column: x => x.ItemLeadTimeId,
                        principalTable: "Inv_ItemLeadTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemLeadTimes_TenantId_ItemId",
                table: "Inv_ItemLeadTimes",
                columns: new[] { "TenantId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemLeadTimeSuppliers_ItemLeadTimeId_SupplierId",
                table: "Inv_ItemLeadTimeSuppliers",
                columns: new[] { "ItemLeadTimeId", "SupplierId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_ItemLeadTimeSuppliers");

            migrationBuilder.DropTable(
                name: "Inv_ItemLeadTimes");
        }
    }
}
