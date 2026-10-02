using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AllowSharedSerialNosAcrossItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_SerialNos_TenantId_SerialNumber",
                table: "Inv_SerialNos");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_TenantId_ItemId_SerialNumber",
                table: "Inv_SerialNos",
                columns: new[] { "TenantId", "ItemId", "SerialNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_SerialNos_TenantId_ItemId_SerialNumber",
                table: "Inv_SerialNos");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_TenantId_SerialNumber",
                table: "Inv_SerialNos",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true);
        }
    }
}
