using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentItemIdx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Sal_SalesOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Sal_SalesInvoiceItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Sal_DeliveryNoteItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Pur_PurchaseReceiptItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Pur_PurchaseOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Idx",
                table: "Pur_PurchaseInvoiceItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Best-effort backfill for existing rows: creation order within each document.
            migrationBuilder.Sql(@"UPDATE ""Sal_SalesOrderItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""SalesOrderId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Sal_SalesOrderItems"") r WHERE x.""Id"" = r.""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Sal_SalesInvoiceItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""SalesInvoiceId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Sal_SalesInvoiceItems"") r WHERE x.""Id"" = r.""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Sal_DeliveryNoteItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""DeliveryNoteId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Sal_DeliveryNoteItems"") r WHERE x.""Id"" = r.""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Pur_PurchaseReceiptItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""PurchaseReceiptId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Pur_PurchaseReceiptItems"") r WHERE x.""Id"" = r.""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Pur_PurchaseOrderItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""PurchaseOrderId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Pur_PurchaseOrderItems"") r WHERE x.""Id"" = r.""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Pur_PurchaseInvoiceItems"" x SET ""Idx"" = r.rn FROM (SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""PurchaseInvoiceId"" ORDER BY ""CreationTime"", ""Id"") - 1 AS rn FROM ""Pur_PurchaseInvoiceItems"") r WHERE x.""Id"" = r.""Id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Sal_SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Sal_SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Sal_DeliveryNoteItems");

            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Pur_PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Pur_PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "Idx",
                table: "Pur_PurchaseInvoiceItems");
        }
    }
}
