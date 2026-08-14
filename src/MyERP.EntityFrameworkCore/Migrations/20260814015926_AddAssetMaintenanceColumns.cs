using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetMaintenanceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignToName",
                table: "Ast_AssetMaintenanceTasks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CertificateRequired",
                table: "Ast_AssetMaintenanceTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AssignToName",
                table: "Ast_AssetMaintenanceLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateDetails",
                table: "Ast_AssetMaintenanceLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Ast_AssetMaintenanceLogs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCertificate",
                table: "Ast_AssetMaintenanceLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Ast_AssetMaintenanceLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignToName",
                table: "Ast_AssetMaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "CertificateRequired",
                table: "Ast_AssetMaintenanceTasks");

            migrationBuilder.DropColumn(
                name: "AssignToName",
                table: "Ast_AssetMaintenanceLogs");

            migrationBuilder.DropColumn(
                name: "CertificateDetails",
                table: "Ast_AssetMaintenanceLogs");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Ast_AssetMaintenanceLogs");

            migrationBuilder.DropColumn(
                name: "HasCertificate",
                table: "Ast_AssetMaintenanceLogs");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Ast_AssetMaintenanceLogs");
        }
    }
}
