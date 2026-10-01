using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialRequestItemProductionPlanReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductionPlanId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductionPlanMrItemId",
                table: "Pur_MaterialRequestItems",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductionPlanId",
                table: "Pur_MaterialRequestItems");

            migrationBuilder.DropColumn(
                name: "ProductionPlanMrItemId",
                table: "Pur_MaterialRequestItems");
        }
    }
}
