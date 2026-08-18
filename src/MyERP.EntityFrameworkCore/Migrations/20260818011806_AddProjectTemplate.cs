using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prj_ProjectTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Prj_ProjectTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prj_ProjectTemplateTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    TaskWeight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ExpectedHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    IsMilestone = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Prj_ProjectTemplateTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_ProjectTemplateTasks_Prj_ProjectTemplates_ProjectTempla~",
                        column: x => x.ProjectTemplateId,
                        principalTable: "Prj_ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_ProjectTemplateTaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectTemplateTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prj_ProjectTemplateTaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_ProjectTemplateTaskDependencies_Prj_ProjectTemplateTask~",
                        column: x => x.ProjectTemplateTaskId,
                        principalTable: "Prj_ProjectTemplateTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ProjectTemplates_TenantId_TemplateName",
                table: "Prj_ProjectTemplates",
                columns: new[] { "TenantId", "TemplateName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ProjectTemplateTaskDependencies_ProjectTemplateTaskId_D~",
                table: "Prj_ProjectTemplateTaskDependencies",
                columns: new[] { "ProjectTemplateTaskId", "DependsOnTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_ProjectTemplateTasks_ProjectTemplateId",
                table: "Prj_ProjectTemplateTasks",
                column: "ProjectTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prj_ProjectTemplateTaskDependencies");

            migrationBuilder.DropTable(
                name: "Prj_ProjectTemplateTasks");

            migrationBuilder.DropTable(
                name: "Prj_ProjectTemplates");
        }
    }
}
