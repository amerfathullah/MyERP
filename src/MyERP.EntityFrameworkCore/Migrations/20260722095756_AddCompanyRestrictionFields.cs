using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyRestrictionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RestrictToCompanies",
                table: "Sal_Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictToCompanies",
                table: "Pur_Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RestrictToCompanies",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppCompanyRestrictionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCompanyRestrictionEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_DeliveryScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ScheduledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DeliveredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_DeliveryScheduleEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCompanyRestrictionEntries_TenantId_ParentId",
                table: "AppCompanyRestrictionEntries",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCompanyRestrictionEntries_TenantId_ParentType_ParentId_C~",
                table: "AppCompanyRestrictionEntries",
                columns: new[] { "TenantId", "ParentType", "ParentId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryScheduleEntries_TenantId_SalesOrderId_SalesOrde~",
                table: "Sal_DeliveryScheduleEntries",
                columns: new[] { "TenantId", "SalesOrderId", "SalesOrderItemId", "ScheduledDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCompanyRestrictionEntries");

            migrationBuilder.DropTable(
                name: "Sal_DeliveryScheduleEntries");

            migrationBuilder.DropColumn(
                name: "RestrictToCompanies",
                table: "Sal_Customers");

            migrationBuilder.DropColumn(
                name: "RestrictToCompanies",
                table: "Pur_Suppliers");

            migrationBuilder.DropColumn(
                name: "RestrictToCompanies",
                table: "Inv_Items");
        }
    }
}
