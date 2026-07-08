using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hr_PayrollEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TotalGrossSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalNetSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Hr_PayrollEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hr_PayrollEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EpfEmployee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SocsoEmployee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EisEmployee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Pcb = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EpfEmployer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SocsoEmployer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EisEmployer = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_Hr_PayrollEntryLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hr_PayrollEntryLines_Hr_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalTable: "Hr_PayrollEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hr_PayrollEntries_TenantId_CompanyId_Year_Month",
                table: "Hr_PayrollEntries",
                columns: new[] { "TenantId", "CompanyId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hr_PayrollEntryLines_PayrollEntryId",
                table: "Hr_PayrollEntryLines",
                column: "PayrollEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hr_PayrollEntryLines");

            migrationBuilder.DropTable(
                name: "Hr_PayrollEntries");
        }
    }
}
