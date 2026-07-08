using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddLhdnSuccessLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EInv_SuccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentUuid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LongId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: true),
                    QrCodeUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInv_SuccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EInv_SuccessLogs_EInv_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "EInv_Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EInv_SuccessLogs_SubmissionId",
                table: "EInv_SuccessLogs",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EInv_SuccessLogs_TenantId_CompanyId_SubmittedAt",
                table: "EInv_SuccessLogs",
                columns: new[] { "TenantId", "CompanyId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EInv_SuccessLogs_TenantId_DocumentUuid",
                table: "EInv_SuccessLogs",
                columns: new[] { "TenantId", "DocumentUuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EInv_SuccessLogs");
        }
    }
}
