using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyERP.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acc_AccountsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnlinkPaymentOnCancellationOfInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    UnlinkAdvancePaymentOnCancellationOfOrder = table.Column<bool>(type: "boolean", nullable: false),
                    DeleteLinkedLedgerEntries = table.Column<bool>(type: "boolean", nullable: false),
                    EnableImmutableLedger = table.Column<bool>(type: "boolean", nullable: false),
                    CheckSupplierInvoiceUniqueness = table.Column<bool>(type: "boolean", nullable: false),
                    AutomaticallyFetchPaymentTerms = table.Column<bool>(type: "boolean", nullable: false),
                    EnableSubscription = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCommonPartyAccounting = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMultiCurrencyInvoicesAgainstSinglePartyAccount = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmBeforeResettingPostingDate = table.Column<bool>(type: "boolean", nullable: false),
                    BookStockExpenseGlEntries = table.Column<bool>(type: "boolean", nullable: false),
                    EnableDiscountsAndMargin = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAccountingDimensions = table.Column<bool>(type: "boolean", nullable: false),
                    MergeSimilarAccountHeads = table.Column<bool>(type: "boolean", nullable: false),
                    BookDeferredEntriesBasedOn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AutomaticallyProcessDeferredAccountingEntry = table.Column<bool>(type: "boolean", nullable: false),
                    BookDeferredEntriesViaJournalEntry = table.Column<bool>(type: "boolean", nullable: false),
                    SubmitJournalEntries = table.Column<bool>(type: "boolean", nullable: false),
                    DetermineAddressTaxCategoryFrom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AddTaxesFromItemTaxTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    AddTaxesFromTaxesAndChargesTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    BookTaxDiscountLoss = table.Column<bool>(type: "boolean", nullable: false),
                    RoundRowWiseTax = table.Column<bool>(type: "boolean", nullable: false),
                    AllowStaleExchangeRates = table.Column<bool>(type: "boolean", nullable: false),
                    StaleDays = table.Column<int>(type: "integer", nullable: false),
                    AutoReconcilePayments = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReconciliationJobTrigger = table.Column<int>(type: "integer", nullable: false),
                    ReconciliationQueueSize = table.Column<int>(type: "integer", nullable: false),
                    OverBillingAllowance = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    CreditControllerRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EnableOverdueBillingThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    RoleAllowedToBypassOverdueBilling = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BookAssetDepreciationEntryAutomatically = table.Column<bool>(type: "boolean", nullable: false),
                    CalculateDeprUsingTotalDays = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAgeingRange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShowBalanceInCoa = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePartyMatching = table.Column<bool>(type: "boolean", nullable: false),
                    EnableFuzzyMatching = table.Column<bool>(type: "boolean", nullable: false),
                    TransferMatchDays = table.Column<int>(type: "integer", nullable: false),
                    CreatePrInDraftStatus = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Acc_AccountsSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acc_AccountsSettings");
        }
    }
}
