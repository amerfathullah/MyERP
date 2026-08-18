using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DunningTypeId",
                table: "Sal_Dunnings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sal_DunningTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DunningTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    DunningFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RateOfInterest = table.Column<decimal>(type: "numeric(8,4)", nullable: false),
                    IncomeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_DunningTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_DunningTypes_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_DunningLetterTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DunningTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsDefaultLanguage = table.Column<bool>(type: "boolean", nullable: false),
                    BodyText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClosingText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Sal_DunningLetterTexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_DunningLetterTexts_Sal_DunningTypes_DunningTypeId",
                        column: x => x.DunningTypeId,
                        principalTable: "Sal_DunningTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DunningLetterTexts_DunningTypeId",
                table: "Sal_DunningLetterTexts",
                column: "DunningTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DunningTypes_CompanyId",
                table: "Sal_DunningTypes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DunningTypes_TenantId_CompanyId_DunningTypeName",
                table: "Sal_DunningTypes",
                columns: new[] { "TenantId", "CompanyId", "DunningTypeName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_DunningLetterTexts");

            migrationBuilder.DropTable(
                name: "Sal_DunningTypes");

            migrationBuilder.DropColumn(
                name: "DunningTypeId",
                table: "Sal_Dunnings");
        }
    }
}
