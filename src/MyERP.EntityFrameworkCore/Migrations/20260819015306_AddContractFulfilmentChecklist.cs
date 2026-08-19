using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFulfilmentChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFulfilmentSatisfied",
                table: "CRM_Contracts");

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfilmentDeadline",
                table: "CRM_Contracts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FulfilmentStatus",
                table: "CRM_Contracts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CRM_ContractFulfilmentChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    Requirement = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Fulfilled = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRM_ContractFulfilmentChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CRM_ContractFulfilmentChecklistItems_CRM_Contracts_Contract~",
                        column: x => x.ContractId,
                        principalTable: "CRM_Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CRM_ContractFulfilmentChecklistItems_ContractId",
                table: "CRM_ContractFulfilmentChecklistItems",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CRM_ContractFulfilmentChecklistItems");

            migrationBuilder.DropColumn(
                name: "FulfilmentDeadline",
                table: "CRM_Contracts");

            migrationBuilder.DropColumn(
                name: "FulfilmentStatus",
                table: "CRM_Contracts");

            migrationBuilder.AddColumn<bool>(
                name: "IsFulfilmentSatisfied",
                table: "CRM_Contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
