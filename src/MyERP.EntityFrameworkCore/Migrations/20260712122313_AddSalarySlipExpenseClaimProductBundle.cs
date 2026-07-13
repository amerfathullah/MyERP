using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSalarySlipExpenseClaimProductBundle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hr_ExpenseClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApprovalStatusBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PayableAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvancePaymentEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalClaimedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalSanctionedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmountReimbursed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Hr_ExpenseClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_ExpenseClaims_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hr_ExpenseClaims_Hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_SalarySlips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SalaryStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayrollEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalWorkingDays = table.Column<int>(type: "integer", nullable: false),
                    PaymentDays = table.Column<int>(type: "integer", nullable: false),
                    LeavesWithoutPay = table.Column<int>(type: "integer", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Hr_SalarySlips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_SalarySlips_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hr_SalarySlips_Hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sal_ProductBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Sal_ProductBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ProductBundles_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_ExpenseClaimDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Hr_ExpenseClaimDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_ExpenseClaimDetails_Hr_ExpenseClaims_ExpenseClaimId",
                        column: x => x.ExpenseClaimId,
                        principalTable: "Hr_ExpenseClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_SalarySlipComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalarySlipId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaryComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    IsStatutory = table.Column<bool>(type: "boolean", nullable: false),
                    SalarySlipId1 = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Hr_SalarySlipComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_SalarySlipComponents_Hr_SalarySlips_SalarySlipId",
                        column: x => x.SalarySlipId,
                        principalTable: "Hr_SalarySlips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hr_SalarySlipComponents_Hr_SalarySlips_SalarySlipId1",
                        column: x => x.SalarySlipId1,
                        principalTable: "Hr_SalarySlips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sal_ProductBundleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Uom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_Sal_ProductBundleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sal_ProductBundleItems_Inv_Items_ComponentItemId",
                        column: x => x.ComponentItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sal_ProductBundleItems_Sal_ProductBundles_ProductBundleId",
                        column: x => x.ProductBundleId,
                        principalTable: "Sal_ProductBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_ExpenseClaimDetails_ExpenseClaimId",
                table: "Hr_ExpenseClaimDetails",
                column: "ExpenseClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_ExpenseClaims_CompanyId",
                table: "Hr_ExpenseClaims",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_ExpenseClaims_EmployeeId",
                table: "Hr_ExpenseClaims",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_ExpenseClaims_TenantId_EmployeeId_Status",
                table: "Hr_ExpenseClaims",
                columns: new[] { "TenantId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalarySlipComponents_SalarySlipId",
                table: "Hr_SalarySlipComponents",
                column: "SalarySlipId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalarySlipComponents_SalarySlipId1",
                table: "Hr_SalarySlipComponents",
                column: "SalarySlipId1");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalarySlips_CompanyId",
                table: "Hr_SalarySlips",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalarySlips_EmployeeId",
                table: "Hr_SalarySlips",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_SalarySlips_TenantId_EmployeeId_StartDate_EndDate",
                table: "Hr_SalarySlips",
                columns: new[] { "TenantId", "EmployeeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProductBundleItems_ComponentItemId",
                table: "Sal_ProductBundleItems",
                column: "ComponentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProductBundleItems_ProductBundleId",
                table: "Sal_ProductBundleItems",
                column: "ProductBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProductBundles_ItemId",
                table: "Sal_ProductBundles",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sal_ProductBundles_TenantId_ItemId_IsActive",
                table: "Sal_ProductBundles",
                columns: new[] { "TenantId", "ItemId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hr_ExpenseClaimDetails");

            migrationBuilder.DropTable(
                name: "Hr_SalarySlipComponents");

            migrationBuilder.DropTable(
                name: "Sal_ProductBundleItems");

            migrationBuilder.DropTable(
                name: "Hr_ExpenseClaims");

            migrationBuilder.DropTable(
                name: "Hr_SalarySlips");

            migrationBuilder.DropTable(
                name: "Sal_ProductBundles");
        }
    }
}
