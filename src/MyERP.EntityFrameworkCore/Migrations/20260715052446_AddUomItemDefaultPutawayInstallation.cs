using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddUomItemDefaultPutawayInstallation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BatchSize",
                table: "Mfg_RoutingOperations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BackflushBasedOn",
                table: "Mfg_BOM",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoutingId",
                table: "Mfg_BOM",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Inv_ItemDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomeAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyingCostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellingCostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultSupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPriceListId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultDiscountPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Inv_ItemDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemDefaults_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_ItemDefaults_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_PutawayRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockCapacity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_PutawayRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_PutawayRules_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_Uoms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MustBeWholeNumber = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_Uoms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sal_InstallationNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InstallationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Sal_InstallationNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_InstallationNotes_Sal_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Sal_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_InstallationNotes_Sal_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalTable: "Sal_DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_InstallationNoteItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SerialNo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InstallationNoteId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_InstallationNoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_InstallationNoteItems_Sal_InstallationNotes_Installatio~",
                        column: x => x.InstallationNoteId,
                        principalTable: "Sal_InstallationNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemDefaults_CompanyId",
                table: "Inv_ItemDefaults",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemDefaults_ItemId",
                table: "Inv_ItemDefaults",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemDefaults_TenantId_ItemId_CompanyId",
                table: "Inv_ItemDefaults",
                columns: new[] { "TenantId", "ItemId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PutawayRules_TenantId_CompanyId_ItemId_WarehouseId",
                table: "Inv_PutawayRules",
                columns: new[] { "TenantId", "CompanyId", "ItemId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PutawayRules_WarehouseId",
                table: "Inv_PutawayRules",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Uoms_TenantId_Name",
                table: "Inv_Uoms",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sal_InstallationNoteItems_InstallationNoteId",
                table: "Sal_InstallationNoteItems",
                column: "InstallationNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_InstallationNotes_CustomerId",
                table: "Sal_InstallationNotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_InstallationNotes_DeliveryNoteId",
                table: "Sal_InstallationNotes",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_InstallationNotes_TenantId_CompanyId_DeliveryNoteId",
                table: "Sal_InstallationNotes",
                columns: new[] { "TenantId", "CompanyId", "DeliveryNoteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_ItemDefaults");

            migrationBuilder.DropTable(
                name: "Inv_PutawayRules");

            migrationBuilder.DropTable(
                name: "Inv_Uoms");

            migrationBuilder.DropTable(
                name: "Sal_InstallationNoteItems");

            migrationBuilder.DropTable(
                name: "Sal_InstallationNotes");

            migrationBuilder.DropColumn(
                name: "BatchSize",
                table: "Mfg_RoutingOperations");

            migrationBuilder.DropColumn(
                name: "BackflushBasedOn",
                table: "Mfg_BOM");

            migrationBuilder.DropColumn(
                name: "RoutingId",
                table: "Mfg_BOM");
        }
    }
}
