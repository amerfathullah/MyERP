using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiCurrencyAndDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountCurrency",
                table: "Acc_JournalEntryLines",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgainstVoucherId",
                table: "Acc_JournalEntryLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgainstVoucherType",
                table: "Acc_JournalEntryLines",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountInAccountCurrency",
                table: "Acc_JournalEntryLines",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Acc_JournalEntryLines",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FinanceBook",
                table: "Acc_JournalEntryLines",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdvance",
                table: "Acc_JournalEntryLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "Acc_JournalEntryLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Acc_DimensionFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountingDimensionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowList = table.Column<bool>(type: "boolean", nullable: false),
                    DimensionValueIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_Acc_DimensionFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_Dimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    HideDisabledValues = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Acc_Dimensions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_GlDimensionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    JournalEntryLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountingDimensionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DimensionFieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DimensionValueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_GlDimensionValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_DimensionFilters_TenantId_AccountingDimensionId_Account~",
                table: "Acc_DimensionFilters",
                columns: new[] { "TenantId", "AccountingDimensionId", "AccountId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_Dimensions_TenantId_DocumentType",
                table: "Acc_Dimensions",
                columns: new[] { "TenantId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acc_GlDimensionValues_TenantId_DimensionFieldName_Dimension~",
                table: "Acc_GlDimensionValues",
                columns: new[] { "TenantId", "DimensionFieldName", "DimensionValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_GlDimensionValues_TenantId_JournalEntryLineId_Accountin~",
                table: "Acc_GlDimensionValues",
                columns: new[] { "TenantId", "JournalEntryLineId", "AccountingDimensionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_DimensionFilters");

            migrationBuilder.DropTable(
                name: "Acc_Dimensions");

            migrationBuilder.DropTable(
                name: "Acc_GlDimensionValues");

            migrationBuilder.DropColumn(
                name: "AccountCurrency",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "AgainstVoucherId",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "AgainstVoucherType",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "AmountInAccountCurrency",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "FinanceBook",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "IsAdvance",
                table: "Acc_JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Acc_JournalEntryLines");
        }
    }
}
