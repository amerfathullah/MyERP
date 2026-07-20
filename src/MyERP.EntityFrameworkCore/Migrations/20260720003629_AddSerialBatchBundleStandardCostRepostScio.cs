using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialBatchBundleStandardCostRepostScio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inv_ItemStandardCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PreviousRate = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RevaluationStockReconciliationId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inv_ItemStandardCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_ItemStandardCosts_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_RepostItemValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PostingTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AllowZeroRate = table.Column<bool>(type: "boolean", nullable: false),
                    RepostGlEntries = table.Column<bool>(type: "boolean", nullable: false),
                    TotalAffectedEntries = table.Column<int>(type: "integer", nullable: false),
                    CurrentIndex = table.Column<int>(type: "integer", nullable: false),
                    ErrorLog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    VoucherType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemsToBeReposted = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsDeduplicated = table.Column<bool>(type: "boolean", nullable: false),
                    DedupRepostId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Inv_RepostItemValuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_RepostItemValuations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_SerialAndBatchBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeOfTransaction = table.Column<int>(type: "integer", nullable: false),
                    TotalQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AvgRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherDetailId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    IsRejected = table.Column<bool>(type: "boolean", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    HasSerialNo = table.Column<bool>(type: "boolean", nullable: false),
                    HasBatchNo = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_SerialAndBatchBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_SerialAndBatchBundles_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingInwardOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubcontractingOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PerReceived = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PerBilled = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Pur_SubcontractingInwardOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingInwardOrders_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_SerialAndBatchEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialAndBatchBundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNo = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IncomingRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockQueue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_SerialAndBatchEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_SerialAndBatchEntries_Inv_SerialAndBatchBundles_SerialA~",
                        column: x => x.SerialAndBatchBundleId,
                        principalTable: "Inv_SerialAndBatchBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SubcontractingInwardOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubcontractingInwardOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BomId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BilledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceCostPerQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Pur_SubcontractingInwardOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SubcontractingInwardOrderItems_Pur_SubcontractingInward~",
                        column: x => x.SubcontractingInwardOrderId,
                        principalTable: "Pur_SubcontractingInwardOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemStandardCosts_CompanyId",
                table: "Inv_ItemStandardCosts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_ItemStandardCosts_TenantId_ItemId_EffectiveDate",
                table: "Inv_ItemStandardCosts",
                columns: new[] { "TenantId", "ItemId", "EffectiveDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_RepostItemValuations_CompanyId",
                table: "Inv_RepostItemValuations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_RepostItemValuations_TenantId_CompanyId_Status",
                table: "Inv_RepostItemValuations",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_RepostItemValuations_TenantId_ItemId_WarehouseId_Postin~",
                table: "Inv_RepostItemValuations",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchBundles_CompanyId",
                table: "Inv_SerialAndBatchBundles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchBundles_TenantId_ItemId_WarehouseId_TypeO~",
                table: "Inv_SerialAndBatchBundles",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "TypeOfTransaction" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchBundles_TenantId_VoucherType_VoucherId",
                table: "Inv_SerialAndBatchBundles",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchEntries_SerialAndBatchBundleId",
                table: "Inv_SerialAndBatchEntries",
                column: "SerialAndBatchBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialAndBatchEntries_TenantId_SerialNo",
                table: "Inv_SerialAndBatchEntries",
                columns: new[] { "TenantId", "SerialNo" },
                filter: "[SerialNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingInwardOrderItems_SubcontractingInwardOrde~",
                table: "Pur_SubcontractingInwardOrderItems",
                column: "SubcontractingInwardOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingInwardOrders_CompanyId",
                table: "Pur_SubcontractingInwardOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingInwardOrders_TenantId_CompanyId_Status",
                table: "Pur_SubcontractingInwardOrders",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SubcontractingInwardOrders_TenantId_OrderNumber",
                table: "Pur_SubcontractingInwardOrders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inv_ItemStandardCosts");

            migrationBuilder.DropTable(
                name: "Inv_RepostItemValuations");

            migrationBuilder.DropTable(
                name: "Inv_SerialAndBatchEntries");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingInwardOrderItems");

            migrationBuilder.DropTable(
                name: "Inv_SerialAndBatchBundles");

            migrationBuilder.DropTable(
                name: "Pur_SubcontractingInwardOrders");
        }
    }
}
