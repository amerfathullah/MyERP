using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkstationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkstationTypeId",
                table: "Mfg_Workstations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mfg_WorkstationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HourRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_WorkstationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_WorkstationTypeCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatingComponent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperatingCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_WorkstationTypeCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_WorkstationTypeCosts_Mfg_WorkstationTypes_WorkstationTy~",
                        column: x => x.WorkstationTypeId,
                        principalTable: "Mfg_WorkstationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_Workstations_WorkstationTypeId",
                table: "Mfg_Workstations",
                column: "WorkstationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkstationTypeCosts_WorkstationTypeId",
                table: "Mfg_WorkstationTypeCosts",
                column: "WorkstationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkstationTypes_TenantId_Name",
                table: "Mfg_WorkstationTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Mfg_Workstations_Mfg_WorkstationTypes_WorkstationTypeId",
                table: "Mfg_Workstations",
                column: "WorkstationTypeId",
                principalTable: "Mfg_WorkstationTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mfg_Workstations_Mfg_WorkstationTypes_WorkstationTypeId",
                table: "Mfg_Workstations");

            migrationBuilder.DropTable(
                name: "Mfg_WorkstationTypeCosts");

            migrationBuilder.DropTable(
                name: "Mfg_WorkstationTypes");

            migrationBuilder.DropIndex(
                name: "IX_Mfg_Workstations_WorkstationTypeId",
                table: "Mfg_Workstations");

            migrationBuilder.DropColumn(
                name: "WorkstationTypeId",
                table: "Mfg_Workstations");
        }
    }
}
