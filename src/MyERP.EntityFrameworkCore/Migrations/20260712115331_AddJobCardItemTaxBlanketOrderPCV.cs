using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardItemTaxBlanketOrderPCV : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_PeriodClosingVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosingAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalClosingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Acc_PeriodClosingVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PeriodClosingVouchers_Acc_Accounts_ClosingAccountId",
                        column: x => x.ClosingAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_PeriodClosingVouchers_Acc_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "Acc_FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_PeriodClosingVouchers_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_JobCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkstationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ForQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CompletedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProcessLossQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalTimeInMins = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WipWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SequenceId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlannedTimeInMins = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_Mfg_JobCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCards_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCards_Mfg_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Mfg_Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCards_Mfg_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "Mfg_WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_BlanketOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_Sal_BlanketOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_BlanketOrders_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tax_ItemTaxTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Tax_ItemTaxTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_ItemTaxTemplates_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_PeriodClosingEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodClosingVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsDebit = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_PeriodClosingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PeriodClosingEntries_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_PeriodClosingEntries_Acc_PeriodClosingVouchers_PeriodCl~",
                        column: x => x.PeriodClosingVoucherId,
                        principalTable: "Acc_PeriodClosingVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mfg_JobCardTimeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TimeInMins = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CompletedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Mfg_JobCardTimeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mfg_JobCardTimeLogs_Mfg_JobCards_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "Mfg_JobCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_BlanketOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlanketOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_BlanketOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_BlanketOrderItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_BlanketOrderItems_Sal_BlanketOrders_BlanketOrderId",
                        column: x => x.BlanketOrderId,
                        principalTable: "Sal_BlanketOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tax_ItemTaxTemplateDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemTaxTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NotApplicable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Tax_ItemTaxTemplateDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tax_ItemTaxTemplateDetails_Acc_Accounts_TaxAccountId",
                        column: x => x.TaxAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tax_ItemTaxTemplateDetails_Tax_ItemTaxTemplates_ItemTaxTemp~",
                        column: x => x.ItemTaxTemplateId,
                        principalTable: "Tax_ItemTaxTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PeriodClosingEntries_AccountId",
                table: "Acc_PeriodClosingEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PeriodClosingEntries_PeriodClosingVoucherId",
                table: "Acc_PeriodClosingEntries",
                column: "PeriodClosingVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PeriodClosingVouchers_ClosingAccountId",
                table: "Acc_PeriodClosingVouchers",
                column: "ClosingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PeriodClosingVouchers_CompanyId",
                table: "Acc_PeriodClosingVouchers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PeriodClosingVouchers_FiscalYearId",
                table: "Acc_PeriodClosingVouchers",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCards_CompanyId",
                table: "Mfg_JobCards",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCards_OperationId",
                table: "Mfg_JobCards",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCards_TenantId_WorkOrderId_OperationId",
                table: "Mfg_JobCards",
                columns: new[] { "TenantId", "WorkOrderId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCards_WorkOrderId",
                table: "Mfg_JobCards",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Mfg_JobCardTimeLogs_JobCardId",
                table: "Mfg_JobCardTimeLogs",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_BlanketOrderItems_BlanketOrderId",
                table: "Sal_BlanketOrderItems",
                column: "BlanketOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_BlanketOrderItems_ItemId",
                table: "Sal_BlanketOrderItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_BlanketOrders_CompanyId",
                table: "Sal_BlanketOrders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_BlanketOrders_TenantId_CompanyId_OrderNumber",
                table: "Sal_BlanketOrders",
                columns: new[] { "TenantId", "CompanyId", "OrderNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ItemTaxTemplateDetails_ItemTaxTemplateId",
                table: "Tax_ItemTaxTemplateDetails",
                column: "ItemTaxTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ItemTaxTemplateDetails_TaxAccountId",
                table: "Tax_ItemTaxTemplateDetails",
                column: "TaxAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ItemTaxTemplates_CompanyId",
                table: "Tax_ItemTaxTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_ItemTaxTemplates_TenantId_CompanyId_Title",
                table: "Tax_ItemTaxTemplates",
                columns: new[] { "TenantId", "CompanyId", "Title" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_PeriodClosingEntries");

            migrationBuilder.DropTable(
                name: "Mfg_JobCardTimeLogs");

            migrationBuilder.DropTable(
                name: "Sal_BlanketOrderItems");

            migrationBuilder.DropTable(
                name: "Tax_ItemTaxTemplateDetails");

            migrationBuilder.DropTable(
                name: "Acc_PeriodClosingVouchers");

            migrationBuilder.DropTable(
                name: "Mfg_JobCards");

            migrationBuilder.DropTable(
                name: "Sal_BlanketOrders");

            migrationBuilder.DropTable(
                name: "Tax_ItemTaxTemplates");
        }
    }
}
