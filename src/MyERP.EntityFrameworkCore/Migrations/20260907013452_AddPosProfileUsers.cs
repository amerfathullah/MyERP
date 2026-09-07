using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPosProfileUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sal_PosProfileUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosProfileUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosProfileUsers_Sal_PosProfiles_PosProfileId",
                        column: x => x.PosProfileId,
                        principalTable: "Sal_PosProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileUsers_PosProfileId",
                table: "Sal_PosProfileUsers",
                column: "PosProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileUsers_TenantId_PosProfileId_UserId",
                table: "Sal_PosProfileUsers",
                columns: new[] { "TenantId", "PosProfileId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosProfileUsers_TenantId_UserId_IsDefault",
                table: "Sal_PosProfileUsers",
                columns: new[] { "TenantId", "UserId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sal_PosProfileUsers");
        }
    }
}
