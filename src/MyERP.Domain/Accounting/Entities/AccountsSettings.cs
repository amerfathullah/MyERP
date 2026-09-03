using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Accounts Settings — global configuration parameters for accounting transactions and reports.
/// Maps to ERPNext accounts/doctype/accounts_settings.
/// </summary>
public class AccountsSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    // Invoice & Billing
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
    public bool MaintainSameInternalTransactionRate { get; set; }
    public string MaintainSameRateAction { get; set; } = "Stop";
    public string? RoleToOverrideStopAction { get; set; }

    // Journals & Deferred Accounting
    public bool MergeSimilarAccountHeads { get; set; }
    public string BookDeferredEntriesBasedOn { get; set; } = "Days";
    public bool AutomaticallyProcessDeferredAccountingEntry { get; set; } = true;
    public bool BookDeferredEntriesViaJournalEntry { get; set; }
    public bool SubmitJournalEntries { get; set; }

    // Taxes
    public string DetermineAddressTaxCategoryFrom { get; set; } = "Billing Address";
    public bool AddTaxesFromItemTaxTemplate { get; set; } = true;
    public bool AddTaxesFromTaxesAndChargesTemplate { get; set; }
    public bool BookTaxDiscountLoss { get; set; }
    public bool RoundRowWiseTax { get; set; }

    // Currency Exchange
    public bool AllowStaleExchangeRates { get; set; } = true;
    public int StaleDays { get; set; } = 1;

    // Payments & Reconciliation
    public bool AutoReconcilePayments { get; set; }
    public int AutoReconciliationJobTrigger { get; set; } = 15;
    public int ReconciliationQueueSize { get; set; } = 5;
    public decimal OverBillingAllowance { get; set; }
    public string? CreditControllerRole { get; set; }
    public bool EnableOverdueBillingThreshold { get; set; }
    public string? RoleAllowedToBypassOverdueBilling { get; set; }

    // Assets
    public bool BookAssetDepreciationEntryAutomatically { get; set; } = true;
    public bool CalculateDeprUsingTotalDays { get; set; }

    // Reports & Banking
    public string DefaultAgeingRange { get; set; } = "30, 60, 90, 120";
    public bool ShowBalanceInCoa { get; set; } = true;
    public bool EnablePartyMatching { get; set; }
    public bool EnableFuzzyMatching { get; set; }
    public int TransferMatchDays { get; set; } = 3;
    public bool CreatePrInDraftStatus { get; set; } = true;

    protected AccountsSettings() { }

    public AccountsSettings(Guid id, Guid? tenantId = null) : base(id)
    {
        TenantId = tenantId;
    }
}
