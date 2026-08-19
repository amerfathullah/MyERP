using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSupplierAndCustomerDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inv_ItemCustomerDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_ItemCustomerDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemCustomerDetails_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_ItemCustomerDetails_Sal_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Sal_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_ItemSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierPartNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inv_ItemSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemSuppliers_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_ItemSuppliers_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemCustomerDetails_CustomerId",
                table: "Inv_ItemCustomerDetails",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemCustomerDetails_ItemId_CustomerId",
                table: "Inv_ItemCustomerDetails",
                columns: new[] { "ItemId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemSuppliers_ItemId_SupplierId",
                table: "Inv_ItemSuppliers",
                columns: new[] { "ItemId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemSuppliers_SupplierId",
                table: "Inv_ItemSuppliers",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_ItemCustomerDetails");

            migrationBuilder.DropTable(
                name: "Inv_ItemSuppliers");
        }
    }
}
