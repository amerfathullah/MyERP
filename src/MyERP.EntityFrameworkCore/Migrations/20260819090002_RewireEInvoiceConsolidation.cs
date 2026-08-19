using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Re-wires EInvoiceConsolidation back into the EF model — same incident as
    /// RewireHrAttendanceAndShifts, found while auditing for other instances of it. Migration
    /// 20260803064747_Added_EInvoiceConsolidation already created this exact table
    /// (EInv_Consolidations) on any database that ran it, but MyERPDbContext had no DbSet or
    /// modelBuilder.Entity&lt;T&gt;() config for it — worse than the HR case, because
    /// EInvoiceConsolidationService.ConsolidateInvoicesAsync (called live from
    /// EInvoiceAppService.ConsolidateInvoicesAsync, a real LHDN B2C consolidation endpoint) has
    /// been injecting IRepository&lt;EInvoiceConsolidation, Guid&gt; this whole time with no model
    /// backing it — an actively broken production code path, not just an unreachable feature.
    /// Written as idempotent guarded SQL for the same reason as the HR migration: a plain
    /// CreateTable here would fail with "relation already exists" on any database that already
    /// ran the original migration. Down() is a no-op — table lifecycle ownership stays with
    /// Added_EInvoiceConsolidation.
    /// </remarks>
    public partial class RewireEInvoiceConsolidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""EInv_Consolidations"" (
                    ""Id"" uuid NOT NULL,
                    ""TenantId"" uuid NULL,
                    ""CompanyId"" uuid NOT NULL,
                    ""ConsolidatedInvoiceId"" uuid NOT NULL,
                    ""OriginalInvoiceId"" uuid NOT NULL,
                    ""ExtraProperties"" text NOT NULL,
                    ""ConcurrencyStamp"" character varying(40) NOT NULL,
                    ""CreationTime"" timestamp without time zone NOT NULL,
                    ""CreatorId"" uuid NULL,
                    CONSTRAINT ""PK_EInv_Consolidations"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_EInv_Consolidations_Sal_SalesInvoices_ConsolidatedInvoiceId"" FOREIGN KEY (""ConsolidatedInvoiceId"") REFERENCES ""Sal_SalesInvoices"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_EInv_Consolidations_Sal_SalesInvoices_OriginalInvoiceId"" FOREIGN KEY (""OriginalInvoiceId"") REFERENCES ""Sal_SalesInvoices"" (""Id"") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ""IX_EInv_Consolidations_ConsolidatedInvoiceId"" ON ""EInv_Consolidations"" (""ConsolidatedInvoiceId"");
                CREATE INDEX IF NOT EXISTS ""IX_EInv_Consolidations_OriginalInvoiceId"" ON ""EInv_Consolidations"" (""OriginalInvoiceId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op — see class remarks.
        }
    }
}
