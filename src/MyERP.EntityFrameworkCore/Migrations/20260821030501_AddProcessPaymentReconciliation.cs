using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessPaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_ProcessPaymentReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivablePayableAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultAdvanceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReconciledCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_ProcessPaymentReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ProcessPaymentReconciliations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ProcessPaymentReconciliations_CompanyId",
                table: "Acc_ProcessPaymentReconciliations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ProcessPaymentReconciliations_TenantId_CompanyId_PartyT~",
                table: "Acc_ProcessPaymentReconciliations",
                columns: new[] { "TenantId", "CompanyId", "PartyType", "PartyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_ProcessPaymentReconciliations");
        }
    }
}
