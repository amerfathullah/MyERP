using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractingBom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingBoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    FinishedGoodId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinishedGoodQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FinishedGoodBomId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinishedGoodUom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ServiceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceItemQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ServiceItemUom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConversionFactor = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
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
                    table.PrimaryKey("PK_Pur_SubcontractingBoms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingBoms_TenantId_FinishedGoodId_IsActive",
                table: "Pur_SubcontractingBoms",
                columns: new[] { "TenantId", "FinishedGoodId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pur_SubcontractingBoms");
        }
    }
}
