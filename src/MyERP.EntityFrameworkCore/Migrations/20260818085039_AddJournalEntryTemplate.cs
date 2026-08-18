using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_JournalEntryTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_JournalEntryTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_JournalEntryTemplates_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_JournalEntryTemplateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDebit = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_JournalEntryTemplateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_JournalEntryTemplateLines_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_JournalEntryTemplateLines_Acc_JournalEntryTemplates_Jou~",
                        column: x => x.JournalEntryTemplateId,
                        principalTable: "Acc_JournalEntryTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntryTemplateLines_AccountId",
                table: "Acc_JournalEntryTemplateLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntryTemplateLines_JournalEntryTemplateId",
                table: "Acc_JournalEntryTemplateLines",
                column: "JournalEntryTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntryTemplates_CompanyId",
                table: "Acc_JournalEntryTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_JournalEntryTemplates_TenantId_CompanyId_TemplateName",
                table: "Acc_JournalEntryTemplates",
                columns: new[] { "TenantId", "CompanyId", "TemplateName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_JournalEntryTemplateLines");

            migrationBuilder.DropTable(
                name: "Acc_JournalEntryTemplates");
        }
    }
}
