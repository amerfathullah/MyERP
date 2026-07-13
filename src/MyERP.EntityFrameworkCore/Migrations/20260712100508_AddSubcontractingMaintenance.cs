using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcontractingMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ast_MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialNoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Periodicity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SalesPersonId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Ast_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_MaintenanceSchedules_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_MaintenanceVisits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MaintenanceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionStatus = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Ast_MaintenanceVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_MaintenanceVisits_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PerReceived = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_Pur_SubcontractingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingOrders_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcontractingOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    NetTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Pur_SubcontractingReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingReceipts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_MaintenanceScheduleDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_MaintenanceScheduleDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_MaintenanceScheduleDetails_Ast_MaintenanceSchedules_Mai~",
                        column: x => x.MaintenanceScheduleId,
                        principalTable: "Ast_MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ast_MaintenanceVisitPurposes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceVisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SerialNoId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkDone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ast_MaintenanceVisitPurposes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ast_MaintenanceVisitPurposes_Ast_MaintenanceVisits_Maintena~",
                        column: x => x.MaintenanceVisitId,
                        principalTable: "Ast_MaintenanceVisits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcontractingOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BomId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_SubcontractingOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingOrderItems_Pur_SubcontractingOrders_Subco~",
                        column: x => x.SubcontractingOrderId,
                        principalTable: "Pur_SubcontractingOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingOrderSuppliedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcontractingOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TransferredQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ConsumedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReserveWarehouseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_SubcontractingOrderSuppliedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingOrderSuppliedItems_Pur_SubcontractingOrde~",
                        column: x => x.SubcontractingOrderId,
                        principalTable: "Pur_SubcontractingOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubcontractingReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_SubcontractingReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingReceiptItems_Pur_SubcontractingReceipts_S~",
                        column: x => x.SubcontractingReceiptId,
                        principalTable: "Pur_SubcontractingReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceScheduleDetails_MaintenanceScheduleId",
                table: "Ast_MaintenanceScheduleDetails",
                column: "MaintenanceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceSchedules_CompanyId",
                table: "Ast_MaintenanceSchedules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceSchedules_TenantId_Status",
                table: "Ast_MaintenanceSchedules",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceVisitPurposes_MaintenanceVisitId",
                table: "Ast_MaintenanceVisitPurposes",
                column: "MaintenanceVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceVisits_CompanyId",
                table: "Ast_MaintenanceVisits",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Ast_MaintenanceVisits_TenantId_CompletionStatus",
                table: "Ast_MaintenanceVisits",
                columns: new[] { "TenantId", "CompletionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrderItems_SubcontractingOrderId",
                table: "Pur_SubcontractingOrderItems",
                column: "SubcontractingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrders_CompanyId",
                table: "Pur_SubcontractingOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrders_TenantId_OrderNumber",
                table: "Pur_SubcontractingOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingOrderSuppliedItems_SubcontractingOrderId",
                table: "Pur_SubcontractingOrderSuppliedItems",
                column: "SubcontractingOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceiptItems_SubcontractingReceiptId",
                table: "Pur_SubcontractingReceiptItems",
                column: "SubcontractingReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceipts_CompanyId",
                table: "Pur_SubcontractingReceipts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingReceipts_TenantId_ReceiptNumber",
                table: "Pur_SubcontractingReceipts",
                columns: new[] { "TenantId", "ReceiptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ast_MaintenanceScheduleDetails");

            migrationBuilder.DropTable(
                name: "Ast_MaintenanceVisitPurposes");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingOrderItems");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingOrderSuppliedItems");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingReceiptItems");

            migrationBuilder.DropTable(
                name: "Ast_MaintenanceSchedules");

            migrationBuilder.DropTable(
                name: "Ast_MaintenanceVisits");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingOrders");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingReceipts");
        }
    }
}
