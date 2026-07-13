using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPickListAssetMovementPaymentRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_PaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentRequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceDoctype = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PaymentGateway = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentEntryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Acc_PaymentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PaymentRequests_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_AssetMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MovementDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Ast_AssetMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_AssetMovements_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ast_AssetMovements_Ast_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Ast_Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_PickLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PickListNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inv_PickLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_PickLists_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_PickListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    StockQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TransferredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    SourceDocumentItemId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inv_PickListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_PickListItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_PickListItems_Inv_PickLists_PickListId",
                        column: x => x.PickListId,
                        principalTable: "Inv_PickLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_PickListItems_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentRequests_CompanyId",
                table: "Acc_PaymentRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentRequests_TenantId_PartyType_PartyId_Status",
                table: "Acc_PaymentRequests",
                columns: new[] { "TenantId", "PartyType", "PartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentRequests_TenantId_ReferenceDoctype_ReferenceId",
                table: "Acc_PaymentRequests",
                columns: new[] { "TenantId", "ReferenceDoctype", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_AssetId",
                table: "Ast_AssetMovements",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_CompanyId",
                table: "Ast_AssetMovements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_AssetMovements_TenantId_AssetId_MovementDate",
                table: "Ast_AssetMovements",
                columns: new[] { "TenantId", "AssetId", "MovementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PickListItems_ItemId",
                table: "Inv_PickListItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PickListItems_PickListId",
                table: "Inv_PickListItems",
                column: "PickListId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PickListItems_WarehouseId",
                table: "Inv_PickListItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PickLists_CompanyId",
                table: "Inv_PickLists",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_PickLists_TenantId_CompanyId_Status",
                table: "Inv_PickLists",
                columns: new[] { "TenantId", "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_PaymentRequests");

            migrationBuilder.DropTable(
                name: "Ast_AssetMovements");

            migrationBuilder.DropTable(
                name: "Inv_PickListItems");

            migrationBuilder.DropTable(
                name: "Inv_PickLists");
        }
    }
}
