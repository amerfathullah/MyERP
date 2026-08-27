using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBisectAccountingStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_BisectAccountingStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Algorithm = table.Column<int>(type: "integer", nullable: false),
                    CurrentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentFromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CurrentToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlSummary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BsSummary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_Acc_BisectAccountingStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_BisectNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BisectAccountingStatementsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeftChildId = table.Column<Guid>(type: "uuid", nullable: true),
                    RightChildId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodFromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PlSummary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BsSummary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsGenerated = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_BisectNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BisectNodes_Acc_BisectAccountingStatements_BisectAccoun~",
                        column: x => x.BisectAccountingStatementsId,
                        principalTable: "Acc_BisectAccountingStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BisectAccountingStatements_TenantId_CompanyId_FromDate_~",
                table: "Acc_BisectAccountingStatements",
                columns: new[] { "TenantId", "CompanyId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BisectNodes_BisectAccountingStatementsId",
                table: "Acc_BisectNodes",
                column: "BisectAccountingStatementsId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BisectNodes_ParentNodeId",
                table: "Acc_BisectNodes",
                column: "ParentNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_BisectNodes");

            migrationBuilder.DropTable(
                name: "Acc_BisectAccountingStatements");
        }
    }
}
