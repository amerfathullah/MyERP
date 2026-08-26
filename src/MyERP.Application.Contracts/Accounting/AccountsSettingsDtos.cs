using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class AccountsSettingsDto : FullAuditedEntityDto<Guid>
{
    // Invoice & Billing
    public bool UnlinkPaymentOnCancellationOfInvoice { get; set; }
    public bool UnlinkAdvancePaymentOnCancellationOfOrder { get; set; }
    public bool DeleteLinkedLedgerEntries { get; set; }
    public bool EnableImmutableLedger { get; set; }
    public bool CheckSupplierInvoiceUniqueness { get; set; }
    public bool AutomaticallyFetchPaymentTerms { get; set; }
    public bool EnableSubscription { get; set; }
    public bool EnableCommonPartyAccounting { get; set; }
    public bool AllowMultiCurrencyInvoicesAgainstSinglePartyAccount { get; set; }
    public bool ConfirmBeforeResettingPostingDate { get; set; }
    public bool BookStockExpenseGlEntries { get; set; }
    public bool EnableDiscountsAndMargin { get; set; }
    public bool EnableAccountingDimensions { get; set; }

    // Journals & Deferred Accounting
    public bool MergeSimilarAccountHeads { get; set; }
    public string BookDeferredEntriesBasedOn { get; set; } = null!;
    public bool AutomaticallyProcessDeferredAccountingEntry { get; set; }
    public bool BookDeferredEntriesViaJournalEntry { get; set; }
    public bool SubmitJournalEntries { get; set; }

    // Taxes
    public string DetermineAddressTaxCategoryFrom { get; set; } = null!;
    public bool AddTaxesFromItemTaxTemplate { get; set; }
    public bool AddTaxesFromTaxesAndChargesTemplate { get; set; }
    public bool BookTaxDiscountLoss { get; set; }
    public bool RoundRowWiseTax { get; set; }

    // Currency Exchange
    public bool AllowStaleExchangeRates { get; set; }
    public int StaleDays { get; set; }

    // Payments & Reconciliation
    public bool AutoReconcilePayments { get; set; }
    public int AutoReconciliationJobTrigger { get; set; }
    public int ReconciliationQueueSize { get; set; }
    public decimal OverBillingAllowance { get; set; }
    public string? CreditControllerRole { get; set; }
    public bool EnableOverdueBillingThreshold { get; set; }
    public string? RoleAllowedToBypassOverdueBilling { get; set; }

    // Assets
    public bool BookAssetDepreciationEntryAutomatically { get; set; }
    public bool CalculateDeprUsingTotalDays { get; set; }

    // Reports & Banking
    public string DefaultAgeingRange { get; set; } = null!;
    public bool ShowBalanceInCoa { get; set; }
    public bool EnablePartyMatching { get; set; }
    public bool EnableFuzzyMatching { get; set; }
    public int TransferMatchDays { get; set; }
    public bool CreatePrInDraftStatus { get; set; }
}

public class UpdateAccountsSettingsDto
{
    public bool UnlinkPaymentOnCancellationOfInvoice { get; set; } = true;
    public bool UnlinkAdvancePaymentOnCancellationOfOrder { get; set; } = true;
    public bool DeleteLinkedLedgerEntries { get; set; }
    public bool EnableImmutableLedger { get; set; }
    public bool CheckSupplierInvoiceUniqueness { get; set; }
    public bool AutomaticallyFetchPaymentTerms { get; set; }
    public bool EnableSubscription { get; set; } = true;
    public bool EnableCommonPartyAccounting { get; set; }
    public bool AllowMultiCurrencyInvoicesAgainstSinglePartyAccount { get; set; }
    public bool ConfirmBeforeResettingPostingDate { get; set; } = true;
    public bool BookStockExpenseGlEntries { get; set; }
    public bool EnableDiscountsAndMargin { get; set; }
    public bool EnableAccountingDimensions { get; set; }

    public bool MergeSimilarAccountHeads { get; set; }
    [StringLength(AccountsSettingsConsts.MaxOptionLength)]
    public string BookDeferredEntriesBasedOn { get; set; } = "Days";
    public bool AutomaticallyProcessDeferredAccountingEntry { get; set; } = true;
    public bool BookDeferredEntriesViaJournalEntry { get; set; }
    public bool SubmitJournalEntries { get; set; }

    [StringLength(AccountsSettingsConsts.MaxOptionLength)]
    public string DetermineAddressTaxCategoryFrom { get; set; } = "Billing Address";
    public bool AddTaxesFromItemTaxTemplate { get; set; } = true;
    public bool AddTaxesFromTaxesAndChargesTemplate { get; set; }
    public bool BookTaxDiscountLoss { get; set; }
    public bool RoundRowWiseTax { get; set; }

    public bool AllowStaleExchangeRates { get; set; } = true;
    public int StaleDays { get; set; } = 1;

    public bool AutoReconcilePayments { get; set; }
    public int AutoReconciliationJobTrigger { get; set; } = 15;
    public int ReconciliationQueueSize { get; set; } = 5;
    public decimal OverBillingAllowance { get; set; }
    [StringLength(AccountsSettingsConsts.MaxRoleLength)]
    public string? CreditControllerRole { get; set; }
    public bool EnableOverdueBillingThreshold { get; set; }
    [StringLength(AccountsSettingsConsts.MaxRoleLength)]
    public string? RoleAllowedToBypassOverdueBilling { get; set; }

    public bool BookAssetDepreciationEntryAutomatically { get; set; } = true;
    public bool CalculateDeprUsingTotalDays { get; set; }

    [StringLength(AccountsSettingsConsts.MaxAgeingRangeLength)]
    public string DefaultAgeingRange { get; set; } = "30, 60, 90, 120";
    public bool ShowBalanceInCoa { get; set; } = true;
    public bool EnablePartyMatching { get; set; }
    public bool EnableFuzzyMatching { get; set; }
    public int TransferMatchDays { get; set; } = 3;
    public bool CreatePrInDraftStatus { get; set; } = true;
}
