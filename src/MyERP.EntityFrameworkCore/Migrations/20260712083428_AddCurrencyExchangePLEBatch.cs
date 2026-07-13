using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyExchangePLEBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_CurrencyExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ToCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,9)", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ForBuying = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsAutoFetched = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_CurrencyExchanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Acc_PaymentLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgainstVoucherType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgainstVoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AmountInAccountCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AccountCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Delinked = table.Column<bool>(type: "boolean", nullable: false),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    CostCenterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acc_PaymentLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acc_PaymentLedgerEntries_Acc_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Acc_Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acc_PaymentLedgerEntries_AppCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "AppCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inv_Batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceDocType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceDocId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ShelfLifeInDays = table.Column<int>(type: "integer", nullable: true),
                    UseBatchwiseValuation = table.Column<bool>(type: "boolean", nullable: false),
                    SupplierBatchNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Inv_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inv_Batches_Inv_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Inv_Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_CurrencyExchanges_TenantId_FromCurrency_ToCurrency_Date",
                table: "Acc_CurrencyExchanges",
                columns: new[] { "TenantId", "FromCurrency", "ToCurrency", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentLedgerEntries_AccountId",
                table: "Acc_PaymentLedgerEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentLedgerEntries_CompanyId",
                table: "Acc_PaymentLedgerEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentLedgerEntries_TenantId_AgainstVoucherType_Agains~",
                table: "Acc_PaymentLedgerEntries",
                columns: new[] { "TenantId", "AgainstVoucherType", "AgainstVoucherId", "Delinked" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentLedgerEntries_TenantId_PartyType_PartyId_Posting~",
                table: "Acc_PaymentLedgerEntries",
                columns: new[] { "TenantId", "PartyType", "PartyId", "PostingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Acc_PaymentLedgerEntries_TenantId_VoucherType_VoucherId",
                table: "Acc_PaymentLedgerEntries",
                columns: new[] { "TenantId", "VoucherType", "VoucherId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Batches_ItemId",
                table: "Inv_Batches",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Batches_TenantId_ExpiryDate",
                table: "Inv_Batches",
                columns: new[] { "TenantId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Inv_Batches_TenantId_ItemId_BatchNo",
                table: "Inv_Batches",
                columns: new[] { "TenantId", "ItemId", "BatchNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_CurrencyExchanges");

            migrationBuilder.DropTable(
                name: "Acc_PaymentLedgerEntries");

            migrationBuilder.DropTable(
                name: "Inv_Batches");
        }
    }
}
