using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentActivityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AmendedFromId",
                table: "Sal_SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendmentIndex",
                table: "Sal_SalesOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "App_ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_App_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_App_ActivityLogs_CompanyId_CreationTime",
                table: "App_ActivityLogs",
                columns: new[] { "CompanyId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_App_ActivityLogs_TenantId_DocumentType_DocumentId",
                table: "App_ActivityLogs",
                columns: new[] { "TenantId", "DocumentType", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "App_ActivityLogs");

            migrationBuilder.DropColumn(
                name: "AmendedFromId",
                table: "Sal_SalesOrders");

            migrationBuilder.DropColumn(
                name: "AmendmentIndex",
                table: "Sal_SalesOrders");
        }
    }
}
