using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddShareManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_Shareholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FolioNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsCompany = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_Shareholders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_Shareholders_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ShareTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Acc_ShareTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ShareBalanceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareholderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNo = table.Column<int>(type: "integer", nullable: false),
                    ToNo = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsCompany = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_ShareBalanceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ShareBalanceEntries_Acc_ShareTypes_ShareTypeId",
                        column: x => x.ShareTypeId,
                        principalTable: "Acc_ShareTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ShareBalanceEntries_Acc_Shareholders_ShareholderId",
                        column: x => x.ShareholderId,
                        principalTable: "Acc_Shareholders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_ShareTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferType = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FromShareholderId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromFolioNo = table.Column<string>(type: "text", nullable: true),
                    ToShareholderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToFolioNo = table.Column<string>(type: "text", nullable: true),
                    ShareTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNo = table.Column<int>(type: "integer", nullable: false),
                    ToNo = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EquityOrLiabilityAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_ShareTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_ShareTransfers_Acc_Accounts_EquityOrLiabilityAccountId",
                        column: x => x.EquityOrLiabilityAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ShareTransfers_Acc_ShareTypes_ShareTypeId",
                        column: x => x.ShareTypeId,
                        principalTable: "Acc_ShareTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_ShareTransfers_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareBalanceEntries_ShareholderId",
                table: "Acc_ShareBalanceEntries",
                column: "ShareholderId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareBalanceEntries_ShareTypeId",
                table: "Acc_ShareBalanceEntries",
                column: "ShareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Shareholders_CompanyId",
                table: "Acc_Shareholders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Shareholders_TenantId_CompanyId_IsCompany",
                table: "Acc_Shareholders",
                columns: new[] { "TenantId", "CompanyId", "IsCompany" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareTransfers_CompanyId",
                table: "Acc_ShareTransfers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareTransfers_EquityOrLiabilityAccountId",
                table: "Acc_ShareTransfers",
                column: "EquityOrLiabilityAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareTransfers_ShareTypeId",
                table: "Acc_ShareTransfers",
                column: "ShareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_ShareTransfers_TenantId_CompanyId_Status",
                table: "Acc_ShareTransfers",
                columns: new[] { "TenantId", "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_ShareBalanceEntries");

            migrationBuilder.DropTable(
                name: "Acc_ShareTransfers");

            migrationBuilder.DropTable(
                name: "Acc_Shareholders");

            migrationBuilder.DropTable(
                name: "Acc_ShareTypes");
        }
    }
}
