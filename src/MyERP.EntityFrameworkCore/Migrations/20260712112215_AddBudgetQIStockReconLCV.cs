using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetQIStockReconLCV : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_Budgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetAgainst = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BudgetAgainstId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetAgainstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAnnualBudgetExceeded = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAccumulatedMonthlyBudgetExceeded = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAnnualBudgetExceededOnPo = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAccumulatedMonthlyBudgetExceededOnPo = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAnnualBudgetExceededOnMr = table.Column<int>(type: "integer", nullable: false),
                    ActionIfAccumulatedMonthlyBudgetExceededOnMr = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Acc_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_Budgets_Acc_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "Acc_FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_Budgets_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_LandedCostVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DistributionMethod = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Inv_LandedCostVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_LandedCostVouchers_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InspectionType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChildRowReference = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SampleSize = table.Column<decimal>(type: "numeric", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocStatus = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ManualInspection = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_QualityInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityInspections_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_QualityInspections_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_StockReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciliationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Inv_StockReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_StockReconciliations_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acc_BudgetAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Acc_BudgetAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_BudgetAccounts_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_BudgetAccounts_Acc_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalTable: "Acc_Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_LandedCostCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandedCostVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Inv_LandedCostCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_LandedCostCharges_Acc_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_LandedCostCharges_Inv_LandedCostVouchers_LandedCostVouc~",
                        column: x => x.LandedCostVoucherId,
                        principalTable: "Inv_LandedCostVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_LandedCostItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandedCostVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApplicableCharges = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Inv_LandedCostItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_LandedCostItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_LandedCostItems_Inv_LandedCostVouchers_LandedCostVouche~",
                        column: x => x.LandedCostVoucherId,
                        principalTable: "Inv_LandedCostVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_QualityInspectionReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QualityInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Specification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MinValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    ReadingValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsNumeric = table.Column<bool>(type: "boolean", nullable: false),
                    FormulaBased = table.Column<bool>(type: "boolean", nullable: false),
                    Formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Inv_QualityInspectionReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_QualityInspectionReadings_Inv_QualityInspections_Qualit~",
                        column: x => x.QualityInspectionId,
                        principalTable: "Inv_QualityInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_StockReconciliationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockReconciliationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrentValuationRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NewValuationRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Inv_StockReconciliationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_StockReconciliationItems_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockReconciliationItems_Inv_StockReconciliations_Stock~",
                        column: x => x.StockReconciliationId,
                        principalTable: "Inv_StockReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_StockReconciliationItems_Inv_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Inv_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BudgetAccounts_AccountId",
                table: "Acc_BudgetAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_BudgetAccounts_BudgetId",
                table: "Acc_BudgetAccounts",
                column: "BudgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Budgets_CompanyId",
                table: "Acc_Budgets",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Budgets_FiscalYearId",
                table: "Acc_Budgets",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Budgets_TenantId_CompanyId_FiscalYearId_BudgetAgainst_B~",
                table: "Acc_Budgets",
                columns: new[] { "TenantId", "CompanyId", "FiscalYearId", "BudgetAgainst", "BudgetAgainstId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostCharges_ExpenseAccountId",
                table: "Inv_LandedCostCharges",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostCharges_LandedCostVoucherId",
                table: "Inv_LandedCostCharges",
                column: "LandedCostVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostItems_ItemId",
                table: "Inv_LandedCostItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostItems_LandedCostVoucherId",
                table: "Inv_LandedCostItems",
                column: "LandedCostVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostVouchers_CompanyId",
                table: "Inv_LandedCostVouchers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_LandedCostVouchers_TenantId_CompanyId_VoucherNumber",
                table: "Inv_LandedCostVouchers",
                columns: new[] { "TenantId", "CompanyId", "VoucherNumber" },
                unique: true,
                filter: "\"VoucherNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspectionReadings_QualityInspectionId",
                table: "Inv_QualityInspectionReadings",
                column: "QualityInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspections_CompanyId",
                table: "Inv_QualityInspections",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspections_ItemId",
                table: "Inv_QualityInspections",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_QualityInspections_TenantId_ReferenceType_ReferenceId",
                table: "Inv_QualityInspections",
                columns: new[] { "TenantId", "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReconciliationItems_ItemId",
                table: "Inv_StockReconciliationItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReconciliationItems_StockReconciliationId",
                table: "Inv_StockReconciliationItems",
                column: "StockReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReconciliationItems_WarehouseId",
                table: "Inv_StockReconciliationItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReconciliations_CompanyId",
                table: "Inv_StockReconciliations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_StockReconciliations_TenantId_CompanyId_PostingDate",
                table: "Inv_StockReconciliations",
                columns: new[] { "TenantId", "CompanyId", "PostingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_BudgetAccounts");

            migrationBuilder.DropTable(
                name: "Inv_LandedCostCharges");

            migrationBuilder.DropTable(
                name: "Inv_LandedCostItems");

            migrationBuilder.DropTable(
                name: "Inv_QualityInspectionReadings");

            migrationBuilder.DropTable(
                name: "Inv_StockReconciliationItems");

            migrationBuilder.DropTable(
                name: "Acc_Budgets");

            migrationBuilder.DropTable(
                name: "Inv_LandedCostVouchers");

            migrationBuilder.DropTable(
                name: "Inv_QualityInspections");

            migrationBuilder.DropTable(
                name: "Inv_StockReconciliations");
        }
    }
}
