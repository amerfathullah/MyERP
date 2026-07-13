using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddTimesheetLeaveSerialNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hr_LeaveApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalLeaveDays = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    HalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LeaveApproverId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Hr_LeaveApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_LeaveApplications_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hr_LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxDaysAllowed = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AllowCarryForward = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCarryForwardDays = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    CarryForwardExpiryMonths = table.Column<int>(type: "integer", nullable: false),
                    IsPaidLeave = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeHolidays = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNegativeBalance = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Hr_LeaveTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inv_SerialNos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PurchaseDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryDocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    WarrantyExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AmcExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MaintenanceStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_Inv_SerialNos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_SerialNos_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inv_SerialNos_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_Timesheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalHours = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBillableHours = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalBillingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCostingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Prj_Timesheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_Timesheets_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prj_TimesheetDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    BillingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostingRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SalesInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prj_TimesheetDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prj_TimesheetDetails_Prj_Timesheets_TimesheetId",
                        column: x => x.TimesheetId,
                        principalTable: "Prj_Timesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_LeaveApplications_CompanyId",
                table: "Hr_LeaveApplications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Hr_LeaveApplications_TenantId_EmployeeId_FromDate_ToDate",
                table: "Hr_LeaveApplications",
                columns: new[] { "TenantId", "EmployeeId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_LeaveApplications_TenantId_Status",
                table: "Hr_LeaveApplications",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_LeaveTypes_TenantId_Name",
                table: "Hr_LeaveTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_CompanyId",
                table: "Inv_SerialNos",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_ItemId",
                table: "Inv_SerialNos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_TenantId_ItemId_WarehouseId_Status",
                table: "Inv_SerialNos",
                columns: new[] { "TenantId", "ItemId", "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_SerialNos_TenantId_SerialNumber",
                table: "Inv_SerialNos",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prj_TimesheetDetails_TimesheetId",
                table: "Prj_TimesheetDetails",
                column: "TimesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Timesheets_CompanyId",
                table: "Prj_Timesheets",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Prj_Timesheets_TenantId_EmployeeId_StartDate",
                table: "Prj_Timesheets",
                columns: new[] { "TenantId", "EmployeeId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hr_LeaveApplications");

            migrationBuilder.DropTable(
                name: "Hr_LeaveTypes");

            migrationBuilder.DropTable(
                name: "Inv_SerialNos");

            migrationBuilder.DropTable(
                name: "Prj_TimesheetDetails");

            migrationBuilder.DropTable(
                name: "Prj_Timesheets");
        }
    }
}
