using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Re-wires Attendance/ShiftType/ShiftAssignment back into the EF model — they had a DbSet and
    /// modelBuilder.Entity&lt;T&gt;() config nowhere in MyERPDbContext, even though migration
    /// 20260803065737_AddHrAttendanceAndShifts already created these exact tables (AppAttendances/
    /// AppShiftTypes/AppShiftAssignments) on any database that ran it. Per the documented incident in
    /// migration-workflow: trusting `dotnet ef migrations add` here would regenerate plain CreateTable
    /// calls that fail with "relation already exists" on such a database, because EF's current model
    /// snapshot had no record of these tables. Written as idempotent guarded SQL instead so it's a
    /// true no-op wherever the original migration already ran, and still creates the tables on a
    /// database where it somehow didn't (e.g. this local dev DB, never migrated past that point).
    /// Down() is intentionally a no-op — table lifecycle ownership (create/drop) stays with the
    /// original AddHrAttendanceAndShifts migration; this migration only fixes the model, never owned
    /// the schema.
    /// </remarks>
    public partial class RewireHrAttendanceAndShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""AppAttendances"" (
                    ""Id"" uuid NOT NULL,
                    ""TenantId"" uuid NULL,
                    ""CompanyId"" uuid NOT NULL,
                    ""EmployeeId"" uuid NOT NULL,
                    ""Date"" timestamp without time zone NOT NULL,
                    ""Status"" integer NOT NULL,
                    ""ShiftTypeId"" uuid NULL,
                    ""InTime"" timestamp without time zone NULL,
                    ""OutTime"" timestamp without time zone NULL,
                    ""LeaveApplicationId"" uuid NULL,
                    ""ExtraProperties"" text NOT NULL,
                    ""ConcurrencyStamp"" character varying(40) NOT NULL,
                    ""CreationTime"" timestamp without time zone NOT NULL,
                    ""CreatorId"" uuid NULL,
                    ""LastModificationTime"" timestamp without time zone NULL,
                    ""LastModifierId"" uuid NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                    ""DeleterId"" uuid NULL,
                    ""DeletionTime"" timestamp without time zone NULL,
                    CONSTRAINT ""PK_AppAttendances"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_AppAttendances_Hr_Employees_EmployeeId"" FOREIGN KEY (""EmployeeId"") REFERENCES ""Hr_Employees"" (""Id"") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ""IX_AppAttendances_EmployeeId"" ON ""AppAttendances"" (""EmployeeId"");
                CREATE INDEX IF NOT EXISTS ""IX_AppAttendances_TenantId_CompanyId_EmployeeId_Date"" ON ""AppAttendances"" (""TenantId"", ""CompanyId"", ""EmployeeId"", ""Date"");

                CREATE TABLE IF NOT EXISTS ""AppShiftTypes"" (
                    ""Id"" uuid NOT NULL,
                    ""TenantId"" uuid NULL,
                    ""CompanyId"" uuid NOT NULL,
                    ""Name"" character varying(100) NOT NULL,
                    ""StartTime"" interval NOT NULL,
                    ""EndTime"" interval NOT NULL,
                    ""HolidayListId"" uuid NULL,
                    ""ExtraProperties"" text NOT NULL,
                    ""ConcurrencyStamp"" character varying(40) NOT NULL,
                    ""CreationTime"" timestamp without time zone NOT NULL,
                    ""CreatorId"" uuid NULL,
                    ""LastModificationTime"" timestamp without time zone NULL,
                    ""LastModifierId"" uuid NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                    ""DeleterId"" uuid NULL,
                    ""DeletionTime"" timestamp without time zone NULL,
                    CONSTRAINT ""PK_AppShiftTypes"" PRIMARY KEY (""Id"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AppShiftTypes_TenantId_CompanyId_Name"" ON ""AppShiftTypes"" (""TenantId"", ""CompanyId"", ""Name"");

                CREATE TABLE IF NOT EXISTS ""AppShiftAssignments"" (
                    ""Id"" uuid NOT NULL,
                    ""TenantId"" uuid NULL,
                    ""CompanyId"" uuid NOT NULL,
                    ""EmployeeId"" uuid NOT NULL,
                    ""ShiftTypeId"" uuid NOT NULL,
                    ""StartDate"" timestamp without time zone NOT NULL,
                    ""EndDate"" timestamp without time zone NULL,
                    ""Status"" integer NOT NULL,
                    ""ExtraProperties"" text NOT NULL,
                    ""ConcurrencyStamp"" character varying(40) NOT NULL,
                    ""CreationTime"" timestamp without time zone NOT NULL,
                    ""CreatorId"" uuid NULL,
                    ""LastModificationTime"" timestamp without time zone NULL,
                    ""LastModifierId"" uuid NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                    ""DeleterId"" uuid NULL,
                    ""DeletionTime"" timestamp without time zone NULL,
                    CONSTRAINT ""PK_AppShiftAssignments"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_AppShiftAssignments_AppShiftTypes_ShiftTypeId"" FOREIGN KEY (""ShiftTypeId"") REFERENCES ""AppShiftTypes"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_AppShiftAssignments_Hr_Employees_EmployeeId"" FOREIGN KEY (""EmployeeId"") REFERENCES ""Hr_Employees"" (""Id"") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ""IX_AppShiftAssignments_EmployeeId"" ON ""AppShiftAssignments"" (""EmployeeId"");
                CREATE INDEX IF NOT EXISTS ""IX_AppShiftAssignments_ShiftTypeId"" ON ""AppShiftAssignments"" (""ShiftTypeId"");
                CREATE INDEX IF NOT EXISTS ""IX_AppShiftAssignments_TenantId_CompanyId_EmployeeId_ShiftType~"" ON ""AppShiftAssignments"" (""TenantId"", ""CompanyId"", ""EmployeeId"", ""ShiftTypeId"", ""StartDate"");
                CREATE INDEX IF NOT EXISTS ""IX_AppShiftAssignments_TenantId_CompanyId_Status"" ON ""AppShiftAssignments"" (""TenantId"", ""CompanyId"", ""Status"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks. This migration never owned table creation,
            // only fixed the model, so it doesn't own dropping them either.
        }
    }
}
