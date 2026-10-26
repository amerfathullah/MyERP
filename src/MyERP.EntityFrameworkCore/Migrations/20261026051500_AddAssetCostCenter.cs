using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCostCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Ast_Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_CostCenterId",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "CostCenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_CostCenterId",
                table: "Ast_Assets",
                columns: new[] { "TenantId", "CompanyId", "CostCenterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_CostCenterId",
                table: "Ast_Assets");

            migrationBuilder.DropIndex(
                name: "IX_Ast_Assets_TenantId_CompanyId_CostCenterId",
                table: "Ast_Assets");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Ast_Assets");
        }
    }
}
