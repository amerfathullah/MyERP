using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialRequestsAndMultiCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseGrandTotal",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseNetTotal",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseTaxAmount",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Sal_SalesInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseGrandTotal",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseNetTotal",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseTaxAmount",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Pur_PurchaseInvoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Inv_Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Pur_MaterialRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RequiredByDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Pur_MaterialRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_MaterialRequests_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_MaterialRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_MaterialRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_MaterialRequestItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_MaterialRequestItems_Pur_MaterialRequests_MaterialReque~",
                        column: x => x.MaterialRequestId,
                        principalTable: "Pur_MaterialRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequestItems_ItemId",
                table: "Pur_MaterialRequestItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequestItems_MaterialRequestId",
                table: "Pur_MaterialRequestItems",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequests_CompanyId",
                table: "Pur_MaterialRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_MaterialRequests_TenantId_CompanyId_RequestNumber",
                table: "Pur_MaterialRequests",
                columns: new[] { "TenantId", "CompanyId", "RequestNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pur_MaterialRequestItems");

            migrationBuilder.DropTable(
                name: "Pur_MaterialRequests");

            migrationBuilder.DropColumn(
                name: "BaseGrandTotal",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BaseNetTotal",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BaseTaxAmount",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "BaseGrandTotal",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BaseNetTotal",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "BaseTaxAmount",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Inv_Items");
        }
    }
}
