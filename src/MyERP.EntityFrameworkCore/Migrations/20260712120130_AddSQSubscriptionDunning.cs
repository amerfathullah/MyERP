using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSQSubscriptionDunning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pur_SupplierQuotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QuotationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidTill = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RequestForQuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Pur_SupplierQuotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierQuotations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierQuotations_Pur_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Pur_Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_Dunnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DunningLevel = table.Column<int>(type: "integer", nullable: false),
                    TotalOutstanding = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DunningFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Sal_Dunnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_Dunnings_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_Dunnings_Sal_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Sal_Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubscriptionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GenerateDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BillingInterval = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BillingIntervalCount = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CurrentInvoiceStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CurrentInvoiceEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DaysUntilDue = table.Column<int>(type: "integer", nullable: false),
                    CancelAfterGraceDays = table.Column<int>(type: "integer", nullable: false),
                    TrialPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    TrialEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalPerInterval = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_Subscriptions_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pur_SupplierQuotationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MaterialRequestItemId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Pur_SupplierQuotationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierQuotationItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pur_SupplierQuotationItems_Pur_SupplierQuotations_SupplierQ~",
                        column: x => x.SupplierQuotationId,
                        principalTable: "Pur_SupplierQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_DunningOverduePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DunningId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OverdueDays = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Sal_DunningOverduePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_DunningOverduePayments_Sal_Dunnings_DunningId",
                        column: x => x.DunningId,
                        principalTable: "Sal_Dunnings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_DunningOverduePayments_Sal_SalesInvoices_SalesInvoiceId",
                        column: x => x.SalesInvoiceId,
                        principalTable: "Sal_SalesInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_SubscriptionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_SubscriptionPlans_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_SubscriptionPlans_Sal_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Sal_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierQuotationItems_ItemId",
                table: "Pur_SupplierQuotationItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierQuotationItems_SupplierQuotationId",
                table: "Pur_SupplierQuotationItems",
                column: "SupplierQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierQuotations_CompanyId",
                table: "Pur_SupplierQuotations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierQuotations_SupplierId",
                table: "Pur_SupplierQuotations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Pur_SupplierQuotations_TenantId_CompanyId_QuotationNumber",
                table: "Pur_SupplierQuotations",
                columns: new[] { "TenantId", "CompanyId", "QuotationNumber" },
                unique: true,
                filter: "\"QuotationNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DunningOverduePayments_DunningId",
                table: "Sal_DunningOverduePayments",
                column: "DunningId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_DunningOverduePayments_SalesInvoiceId",
                table: "Sal_DunningOverduePayments",
                column: "SalesInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Dunnings_CompanyId",
                table: "Sal_Dunnings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Dunnings_CustomerId",
                table: "Sal_Dunnings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Dunnings_TenantId_CustomerId_DunningLevel",
                table: "Sal_Dunnings",
                columns: new[] { "TenantId", "CustomerId", "DunningLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SubscriptionPlans_ItemId",
                table: "Sal_SubscriptionPlans",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_SubscriptionPlans_SubscriptionId",
                table: "Sal_SubscriptionPlans",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Subscriptions_CompanyId",
                table: "Sal_Subscriptions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_Subscriptions_TenantId_CompanyId_SubscriptionNumber",
                table: "Sal_Subscriptions",
                columns: new[] { "TenantId", "CompanyId", "SubscriptionNumber" },
                unique: true,
                filter: "\"SubscriptionNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pur_SupplierQuotationItems");

            migrationBuilder.DropTable(
                name: "Sal_DunningOverduePayments");

            migrationBuilder.DropTable(
                name: "Sal_SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "Pur_SupplierQuotations");

            migrationBuilder.DropTable(
                name: "Sal_Dunnings");

            migrationBuilder.DropTable(
                name: "Sal_Subscriptions");
        }
    }
}
