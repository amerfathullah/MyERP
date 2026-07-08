using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class InitializeMyERP3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pur_PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierDeliveryNote = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsReturn = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnAgainstId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_PurchaseReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_PurchaseReceipts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_PurchaseReceipts_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_PurchaseReceipts_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_DeliveryNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Transporter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsReturn = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnAgainstId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_DeliveryNotes_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_DeliveryNotes_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_DeliveryNotes_Sal_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Sal_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_PurchaseReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_PurchaseReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_PurchaseReceiptItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_PurchaseReceiptItems_Pur_PurchaseReceipts_PurchaseRecei~",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "Pur_PurchaseReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_DeliveryNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SalesOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_DeliveryNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_DeliveryNoteItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_DeliveryNoteItems_Sal_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "Sal_DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceiptItems_ItemId",
                table: "Pur_PurchaseReceiptItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceiptItems_PurchaseReceiptId",
                table: "Pur_PurchaseReceiptItems",
                column: "PurchaseReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceipts_CompanyId",
                table: "Pur_PurchaseReceipts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceipts_SupplierId",
                table: "Pur_PurchaseReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceipts_TenantId_CompanyId_ReceiptNumber",
                table: "Pur_PurchaseReceipts",
                columns: new[] { "TenantId", "CompanyId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_PurchaseReceipts_WarehouseId",
                table: "Pur_PurchaseReceipts",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNoteItems_DeliveryNoteId",
                table: "Sal_DeliveryNoteItems",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNoteItems_ItemId",
                table: "Sal_DeliveryNoteItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_CompanyId",
                table: "Sal_DeliveryNotes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_CustomerId",
                table: "Sal_DeliveryNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_TenantId_CompanyId_DeliveryNumber",
                table: "Sal_DeliveryNotes",
                columns: new[] { "TenantId", "CompanyId", "DeliveryNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DeliveryNotes_WarehouseId",
                table: "Sal_DeliveryNotes",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropTable(
                name: "Sal_DeliveryNoteItems");

            migrationBuilder.DropTable(
                name: "Pur_PurchaseReceipts");

            migrationBuilder.DropTable(
                name: "Sal_DeliveryNotes");
        }
    }
}
