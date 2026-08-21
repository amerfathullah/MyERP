using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddRepostAccountingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_RepostAccountingLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorLog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Acc_RepostAccountingLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_RepostAccountingLedgers_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_RepostAccountingLedgerVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepostAccountingLedgerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_RepostAccountingLedgerVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_RepostAccountingLedgerVouchers_Acc_RepostAccountingLedg~",
                        column: x => x.RepostAccountingLedgerId,
                        principalTable: "Acc_RepostAccountingLedgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_RepostAccountingLedgers_CompanyId",
                table: "Acc_RepostAccountingLedgers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_RepostAccountingLedgers_TenantId_CompanyId_Status",
                table: "Acc_RepostAccountingLedgers",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_RepostAccountingLedgerVouchers_RepostAccountingLedgerId",
                table: "Acc_RepostAccountingLedgerVouchers",
                column: "RepostAccountingLedgerId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_RepostAccountingLedgerVouchers_VoucherType_VoucherId",
                table: "Acc_RepostAccountingLedgerVouchers",
                columns: new[] { "VoucherType", "VoucherId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_RepostAccountingLedgerVouchers");

            migrationBuilder.DropTable(
                name: "Acc_RepostAccountingLedgers");
        }
    }
}
