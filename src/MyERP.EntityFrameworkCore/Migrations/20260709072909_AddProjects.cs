using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prj_Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    PercentCompleteMethod = table.Column<int>(type: "integer", nullable: false),
                    PercentComplete = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpectedEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCostingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBillingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBilledAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CostCenter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_Prj_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prj_Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGroup = table.Column<bool>(type: "boolean", nullable: false),
                    IsMilestone = table.Column<bool>(type: "boolean", nullable: false),
                    TaskWeight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Progress = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    ExpectedStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpectedEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpectedHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    ActualHours = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Prj_Tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_Tasks_Prj_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Prj_Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_TaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prj_TaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_TaskDependencies_Prj_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Prj_Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Projects_TenantId_ProjectNumber",
                table: "Prj_Projects",
                columns: new[] { "TenantId", "ProjectNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Projects_TenantId_Status",
                table: "Prj_Projects",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Prj_TaskDependencies_TaskId_DependsOnTaskId",
                table: "Prj_TaskDependencies",
                columns: new[] { "TaskId", "DependsOnTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Tasks_ProjectId_Status",
                table: "Prj_Tasks",
                columns: new[] { "ProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prj_TaskDependencies");

            migrationBuilder.DropTable(
                name: "Prj_Tasks");

            migrationBuilder.DropTable(
                name: "Prj_Projects");
        }
    }
}
