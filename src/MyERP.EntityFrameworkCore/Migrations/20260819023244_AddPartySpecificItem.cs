using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPartySpecificItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sal_PartySpecificItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyType = table.Column<int>(type: "integer", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestrictBasedOn = table.Column<int>(type: "integer", nullable: false),
                    BasedOnValueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PartySpecificItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PartySpecificItems_TenantId_PartyType_PartyId",
                table: "Sal_PartySpecificItems",
                columns: new[] { "TenantId", "PartyType", "PartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PartySpecificItems_TenantId_PartyType_PartyId_RestrictB~",
                table: "Sal_PartySpecificItems",
                columns: new[] { "TenantId", "PartyType", "PartyId", "RestrictBasedOn", "BasedOnValueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_PartySpecificItems");
        }
    }
}
