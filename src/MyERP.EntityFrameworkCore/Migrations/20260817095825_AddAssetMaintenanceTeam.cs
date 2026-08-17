using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMaintenanceTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ast_AssetMaintenanceTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    MaintenanceManagerId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMaintenanceTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMaintenanceTeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetMaintenanceTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRole = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMaintenanceTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetMaintenanceTeamMembers_Ast_AssetMaintenanceTeams_A~",
                        column: x => x.AssetMaintenanceTeamId,
                        principalTable: "Ast_AssetMaintenanceTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceTeamMembers_AssetMaintenanceTeamId",
                table: "Ast_AssetMaintenanceTeamMembers",
                column: "AssetMaintenanceTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMaintenanceTeams_TenantId_CompanyId_TeamName",
                table: "Ast_AssetMaintenanceTeams",
                columns: new[] { "TenantId", "CompanyId", "TeamName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_AssetMaintenanceTeamMembers");

            migrationBuilder.DropTable(
                name: "Ast_AssetMaintenanceTeams");
        }
    }
}
