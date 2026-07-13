using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemPriceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_ItemPrices_TenantId_ItemId_PriceListId_Uom_ValidFrom_Cu~",
                table: "Inv_ItemPrices");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemPrices_TenantId_ItemId_PriceListId_Uom_ValidFrom_Cu~",
                table: "Inv_ItemPrices",
                columns: new[] { "TenantId", "ItemId", "PriceListId", "Uom", "ValidFrom", "CustomerId", "SupplierId", "BatchNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inv_ItemPrices_TenantId_ItemId_PriceListId_Uom_ValidFrom_Cu~",
                table: "Inv_ItemPrices");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemPrices_TenantId_ItemId_PriceListId_Uom_ValidFrom_Cu~",
                table: "Inv_ItemPrices",
                columns: new[] { "TenantId", "ItemId", "PriceListId", "Uom", "ValidFrom", "CustomerId", "SupplierId", "BatchNo" });
        }
    }
}
