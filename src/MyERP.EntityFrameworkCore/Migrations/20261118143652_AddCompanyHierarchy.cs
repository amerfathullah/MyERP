using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGroup",
                table: "AppCompanies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCompanyId",
                table: "AppCompanies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCompanies_ParentCompanyId",
                table: "AppCompanies",
                column: "ParentCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppCompanies_AppCompanies_ParentCompanyId",
                table: "AppCompanies",
                column: "ParentCompanyId",
                principalTable: "AppCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppCompanies_AppCompanies_ParentCompanyId",
                table: "AppCompanies");

            migrationBuilder.DropIndex(
                name: "IX_AppCompanies_ParentCompanyId",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "IsGroup",
                table: "AppCompanies");

            migrationBuilder.DropColumn(
                name: "ParentCompanyId",
                table: "AppCompanies");
        }
    }
}
