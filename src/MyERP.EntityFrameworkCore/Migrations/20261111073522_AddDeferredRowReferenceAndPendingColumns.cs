using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeferredRowReferenceAndPendingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These six columns belong to entity properties added after the InitialBaseline squash
            // without a migration of their own, so whether a given database already has them is
            // not something migration history can tell. Guarded so the migration applies cleanly
            // either way.
            migrationBuilder.Sql("""
                ALTER TABLE "Sal_SalesInvoices" ADD COLUMN IF NOT EXISTS "OriginalInvoiceNumber" character varying(100) NULL;
                ALTER TABLE "Sal_SalesInvoices" ADD COLUMN IF NOT EXISTS "OriginalInvoiceUuid" character varying(50) NULL;
                ALTER TABLE "Sal_DeliveryNoteItems" ADD COLUMN IF NOT EXISTS "SerialAndBatchBundleId" uuid NULL;
                ALTER TABLE "Pur_PurchaseInvoices" ADD COLUMN IF NOT EXISTS "OriginalInvoiceNumber" character varying(100) NULL;
                ALTER TABLE "Pur_PurchaseInvoices" ADD COLUMN IF NOT EXISTS "OriginalInvoiceUuid" character varying(50) NULL;
                ALTER TABLE "Prj_Timesheets" ADD COLUMN IF NOT EXISTS "Title" character varying(200) NULL;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceDetailId",
                table: "Acc_JournalEntryLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalInvoiceNumber",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceUuid",
                table: "Sal_SalesInvoices");

            migrationBuilder.DropColumn(
                name: "SerialAndBatchBundleId",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceNumber",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceUuid",
                table: "Pur_PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "ReferenceDetailId",
                table: "Acc_JournalEntryLines");
        }
    }
}
