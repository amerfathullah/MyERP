using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkTransactionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auto_BulkTransactionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalEntries = table.Column<int>(type: "integer", nullable: false),
                    SucceededCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Auto_BulkTransactionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auto_BulkTransactionLogDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BulkTransactionLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FromDocType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ToDocType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExecutedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetriedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Auto_BulkTransactionLogDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auto_BulkTransactionLogDetails_Auto_BulkTransactionLogs_Bul~",
                        column: x => x.BulkTransactionLogId,
                        principalTable: "Auto_BulkTransactionLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auto_BulkTransactionLogDetails_BulkTransactionLogId",
                table: "Auto_BulkTransactionLogDetails",
                column: "BulkTransactionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Auto_BulkTransactionLogDetails_TenantId_TransactionName",
                table: "Auto_BulkTransactionLogDetails",
                columns: new[] { "TenantId", "TransactionName" });

            migrationBuilder.CreateIndex(
                name: "IX_Auto_BulkTransactionLogs_TenantId_BatchDate",
                table: "Auto_BulkTransactionLogs",
                columns: new[] { "TenantId", "BatchDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auto_BulkTransactionLogDetails");

            migrationBuilder.DropTable(
                name: "Auto_BulkTransactionLogs");
        }
    }
}
