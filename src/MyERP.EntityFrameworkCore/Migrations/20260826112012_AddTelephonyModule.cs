using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTelephonyModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tel_CallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CallId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    From = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    To = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CallDirection = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RecordingUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Medium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CallReceivedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TelephonyCallTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Tel_CallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tel_CallTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CallTypeName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Tel_CallTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tel_IncomingCallSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CallRouting = table.Column<int>(type: "integer", nullable: false),
                    GreetingMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgentBusyMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgentUnavailableMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Tel_IncomingCallSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tel_IncomingCallHandlingSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomingCallSettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    FromTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ToTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EmployeeGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tel_IncomingCallHandlingSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tel_IncomingCallHandlingSchedules_Tel_IncomingCallSettings_~",
                        column: x => x.IncomingCallSettingsId,
                        principalTable: "Tel_IncomingCallSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallLogs_TenantId_CallId",
                table: "Tel_CallLogs",
                columns: new[] { "TenantId", "CallId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallLogs_TenantId_CustomerId",
                table: "Tel_CallLogs",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallLogs_TenantId_From",
                table: "Tel_CallLogs",
                columns: new[] { "TenantId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallLogs_TenantId_Status",
                table: "Tel_CallLogs",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallLogs_TenantId_To",
                table: "Tel_CallLogs",
                columns: new[] { "TenantId", "To" });

            migrationBuilder.CreateIndex(
                name: "IX_Tel_CallTypes_TenantId_CallTypeName",
                table: "Tel_CallTypes",
                columns: new[] { "TenantId", "CallTypeName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tel_IncomingCallHandlingSchedules_EmployeeGroupId",
                table: "Tel_IncomingCallHandlingSchedules",
                column: "EmployeeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Tel_IncomingCallHandlingSchedules_IncomingCallSettingsId",
                table: "Tel_IncomingCallHandlingSchedules",
                column: "IncomingCallSettingsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tel_CallLogs");

            migrationBuilder.DropTable(
                name: "Tel_CallTypes");

            migrationBuilder.DropTable(
                name: "Tel_IncomingCallHandlingSchedules");

            migrationBuilder.DropTable(
                name: "Tel_IncomingCallSettings");
        }
    }
}
