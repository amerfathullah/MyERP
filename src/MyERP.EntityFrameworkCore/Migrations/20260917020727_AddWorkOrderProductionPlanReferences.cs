using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderProductionPlanReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductionPlanId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductionPlanItemId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductionPlanSubAssemblyItemId",
                table: "Mfg_WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_WorkOrders_TenantId_ProductionPlanId",
                table: "Mfg_WorkOrders",
                columns: new[] { "TenantId", "ProductionPlanId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mfg_WorkOrders_TenantId_ProductionPlanId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ProductionPlanId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ProductionPlanItemId",
                table: "Mfg_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ProductionPlanSubAssemblyItemId",
                table: "Mfg_WorkOrders");
        }
    }
}
