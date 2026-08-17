using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportSlaEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgreementStatus",
                table: "Sup_Issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Sup_IssuePriorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Sup_IssuePriorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sup_IssueTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Sup_IssueTypes", x => x.Id);
                });

            // NOTE: Sup_ServiceLevelAgreements was created by 20260803070525_AddSupportModule per
            // migration history, but that table was found NOT to exist on at least one real database
            // (history/schema drift — the table must have been dropped or the migration never truly
            // applied there). This block is written idempotently so it works whether the table already
            // exists (just adds the new SLA columns/FK/index) or needs to be created from scratch.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Sup_ServiceLevelAgreements"" (
                    ""Id"" uuid NOT NULL,
                    ""TenantId"" uuid NULL,
                    ""CompanyId"" uuid NOT NULL,
                    ""Name"" character varying(100) NOT NULL,
                    ""CustomerGroupId"" uuid NULL,
                    ""HolidayListId"" uuid NULL,
                    ""ResolutionTimeHours"" integer NOT NULL,
                    ""ResponseTimeHours"" integer NOT NULL,
                    ""ExtraProperties"" text NOT NULL,
                    ""ConcurrencyStamp"" character varying(40) NOT NULL,
                    ""CreationTime"" timestamp without time zone NOT NULL,
                    ""CreatorId"" uuid NULL,
                    ""LastModificationTime"" timestamp without time zone NULL,
                    ""LastModifierId"" uuid NULL,
                    ""IsDeleted"" boolean NOT NULL DEFAULT FALSE,
                    ""DeleterId"" uuid NULL,
                    ""DeletionTime"" timestamp without time zone NULL,
                    CONSTRAINT ""PK_Sup_ServiceLevelAgreements"" PRIMARY KEY (""Id"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Sup_ServiceLevelAgreements_TenantId_CompanyId_Name""
                    ON ""Sup_ServiceLevelAgreements"" (""TenantId"", ""CompanyId"", ""Name"");

                ALTER TABLE ""Sup_ServiceLevelAgreements"" ADD COLUMN IF NOT EXISTS ""EntityType"" character varying(50) NULL;
                ALTER TABLE ""Sup_ServiceLevelAgreements"" ADD COLUMN IF NOT EXISTS ""EntityId"" uuid NULL;
                ALTER TABLE ""Sup_ServiceLevelAgreements"" ADD COLUMN IF NOT EXISTS ""IsDefault"" boolean NOT NULL DEFAULT FALSE;
                ALTER TABLE ""Sup_ServiceLevelAgreements"" ADD COLUMN IF NOT EXISTS ""ApplyOnResolution"" boolean NOT NULL DEFAULT TRUE;
                ALTER TABLE ""Sup_ServiceLevelAgreements"" ADD COLUMN IF NOT EXISTS ""IsActive"" boolean NOT NULL DEFAULT TRUE;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Sup_ServiceLevelAgreements_AppCompanies_CompanyId'
                    ) THEN
                        ALTER TABLE ""Sup_ServiceLevelAgreements""
                            ADD CONSTRAINT ""FK_Sup_ServiceLevelAgreements_AppCompanies_CompanyId""
                            FOREIGN KEY (""CompanyId"") REFERENCES ""AppCompanies"" (""Id"") ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            migrationBuilder.CreateTable(
                name: "Sup_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackServiceLevelAgreement = table.Column<bool>(type: "boolean", nullable: false),
                    AllowResettingServiceLevelAgreement = table.Column<bool>(type: "boolean", nullable: false),
                    CloseIssueAfterDays = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_Sup_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sup_Settings_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sup_ServiceDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceLevelAgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sup_ServiceDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sup_ServiceDays_Sup_ServiceLevelAgreements_ServiceLevelAgre~",
                        column: x => x.ServiceLevelAgreementId,
                        principalTable: "Sup_ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sup_ServiceLevelPriorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceLevelAgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriorityName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResponseTimeHours = table.Column<decimal>(type: "numeric", nullable: false),
                    ResolutionTimeHours = table.Column<decimal>(type: "numeric", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sup_ServiceLevelPriorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sup_ServiceLevelPriorities_Sup_ServiceLevelAgreements_Servi~",
                        column: x => x.ServiceLevelAgreementId,
                        principalTable: "Sup_ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sup_IssuePriorities_TenantId_Name",
                table: "Sup_IssuePriorities",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sup_IssueTypes_TenantId_Name",
                table: "Sup_IssueTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sup_ServiceDays_ServiceLevelAgreementId",
                table: "Sup_ServiceDays",
                column: "ServiceLevelAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_Sup_ServiceLevelAgreements_CompanyId",
                table: "Sup_ServiceLevelAgreements",
                column: "CompanyId");

            // IX_Sup_ServiceLevelAgreements_TenantId_CompanyId_Name is created (idempotently) in the raw-SQL block above.

            migrationBuilder.CreateIndex(
                name: "IX_Sup_ServiceLevelPriorities_ServiceLevelAgreementId",
                table: "Sup_ServiceLevelPriorities",
                column: "ServiceLevelAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_Sup_Settings_CompanyId",
                table: "Sup_Settings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sup_Settings_TenantId_CompanyId",
                table: "Sup_Settings",
                columns: new[] { "TenantId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sup_IssuePriorities");

            migrationBuilder.DropTable(
                name: "Sup_IssueTypes");

            migrationBuilder.DropTable(
                name: "Sup_ServiceDays");

            migrationBuilder.DropTable(
                name: "Sup_ServiceLevelPriorities");

            migrationBuilder.DropTable(
                name: "Sup_Settings");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP CONSTRAINT IF EXISTS ""FK_Sup_ServiceLevelAgreements_AppCompanies_CompanyId"";
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP COLUMN IF EXISTS ""EntityType"";
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP COLUMN IF EXISTS ""EntityId"";
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP COLUMN IF EXISTS ""IsDefault"";
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP COLUMN IF EXISTS ""ApplyOnResolution"";
                ALTER TABLE ""Sup_ServiceLevelAgreements"" DROP COLUMN IF EXISTS ""IsActive"";
            ");

            migrationBuilder.DropColumn(
                name: "AgreementStatus",
                table: "Sup_Issues");
        }
    }
}
