using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auto_Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConditionExpression = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActionConfig = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Auto_Rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auto_ExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutomationRuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceDocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExecutionDurationMs = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auto_ExecutionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auto_ExecutionLogs_Auto_Rules_AutomationRuleId",
                        column: x => x.AutomationRuleId,
                        principalTable: "Auto_Rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auto_ExecutionLogs_AutomationRuleId",
                table: "Auto_ExecutionLogs",
                column: "AutomationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Auto_ExecutionLogs_TenantId_AutomationRuleId_CreationTime",
                table: "Auto_ExecutionLogs",
                columns: new[] { "TenantId", "AutomationRuleId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Auto_Rules_TenantId_Trigger_IsActive",
                table: "Auto_Rules",
                columns: new[] { "TenantId", "Trigger", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auto_ExecutionLogs");

            migrationBuilder.DropTable(
                name: "Auto_Rules");
        }
    }
}
