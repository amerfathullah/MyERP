using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPosProfileItemGroupsAndCustomerGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sal_PosProfileCustomerGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosProfileCustomerGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosProfileCustomerGroups_Sal_PosProfiles_PosProfileId",
                        column: x => x.PosProfileId,
                        principalTable: "Sal_PosProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosProfileItemGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosProfileItemGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosProfileItemGroups_Sal_PosProfiles_PosProfileId",
                        column: x => x.PosProfileId,
                        principalTable: "Sal_PosProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileCustomerGroups_PosProfileId",
                table: "Sal_PosProfileCustomerGroups",
                column: "PosProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileCustomerGroups_TenantId_PosProfileId_Customer~",
                table: "Sal_PosProfileCustomerGroups",
                columns: new[] { "TenantId", "PosProfileId", "CustomerGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileItemGroups_PosProfileId",
                table: "Sal_PosProfileItemGroups",
                column: "PosProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileItemGroups_TenantId_PosProfileId_ItemGroupId",
                table: "Sal_PosProfileItemGroups",
                columns: new[] { "TenantId", "PosProfileId", "ItemGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_PosProfileCustomerGroups");

            migrationBuilder.DropTable(
                name: "Sal_PosProfileItemGroups");
        }
    }
}
