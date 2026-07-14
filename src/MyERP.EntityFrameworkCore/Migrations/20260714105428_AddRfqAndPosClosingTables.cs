using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqAndPosClosingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pur_RequestForQuotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MessageForSupplier = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Pur_RequestForQuotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_RequestForQuotations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosClosingEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosOpeningEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PostingTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTaxes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConsolidatedSalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Sal_PosClosingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosClosingEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_RfqItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestForQuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialRequestItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_RfqItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_RfqItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_RfqItems_Pur_RequestForQuotations_RequestForQuotationId",
                        column: x => x.RequestForQuotationId,
                        principalTable: "Pur_RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_RfqSuppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestForQuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    QuoteStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pur_RfqSuppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_RfqSuppliers_Pur_RequestForQuotations_RequestForQuotati~",
                        column: x => x.RequestForQuotationId,
                        principalTable: "Pur_RequestForQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_RfqSuppliers_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosClosingInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PosClosingEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosClosingInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosClosingInvoices_Sal_PosClosingEntries_PosClosingEntr~",
                        column: x => x.PosClosingEntryId,
                        principalTable: "Sal_PosClosingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_PosClosingPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PosClosingEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeOfPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sal_PosClosingPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_PosClosingPayments_Sal_PosClosingEntries_PosClosingEntr~",
                        column: x => x.PosClosingEntryId,
                        principalTable: "Sal_PosClosingEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RequestForQuotations_CompanyId",
                table: "Pur_RequestForQuotations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RequestForQuotations_TenantId_CompanyId_RfqNumber",
                table: "Pur_RequestForQuotations",
                columns: new[] { "TenantId", "CompanyId", "RfqNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RfqItems_ItemId",
                table: "Pur_RfqItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RfqItems_RequestForQuotationId",
                table: "Pur_RfqItems",
                column: "RequestForQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RfqSuppliers_RequestForQuotationId",
                table: "Pur_RfqSuppliers",
                column: "RequestForQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_RfqSuppliers_SupplierId",
                table: "Pur_RfqSuppliers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosClosingEntries_CompanyId",
                table: "Sal_PosClosingEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosClosingEntries_TenantId_CompanyId_PosProfileId_Status",
                table: "Sal_PosClosingEntries",
                columns: new[] { "TenantId", "CompanyId", "PosProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosClosingInvoices_PosClosingEntryId",
                table: "Sal_PosClosingInvoices",
                column: "PosClosingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_PosClosingPayments_PosClosingEntryId",
                table: "Sal_PosClosingPayments",
                column: "PosClosingEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pur_RfqItems");

            migrationBuilder.DropTable(
                name: "Pur_RfqSuppliers");

            migrationBuilder.DropTable(
                name: "Sal_PosClosingInvoices");

            migrationBuilder.DropTable(
                name: "Sal_PosClosingPayments");

            migrationBuilder.DropTable(
                name: "Pur_RequestForQuotations");

            migrationBuilder.DropTable(
                name: "Sal_PosClosingEntries");
        }
    }
}
