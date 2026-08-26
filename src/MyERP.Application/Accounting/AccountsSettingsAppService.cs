using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.AccountsSettings.Default)]
public class AccountsSettingsAppService : MyERPAppService, IAccountsSettingsAppService
{
    private readonly IRepository<AccountsSettings, Guid> _repository;

    public AccountsSettingsAppService(IRepository<AccountsSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AccountsSettingsDto> GetAsync()
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new AccountsSettings(GuidGenerator.Create(), CurrentTenant.Id);
            await _repository.InsertAsync(settings);
        }

        return new AccountsSettingsMapper().Map(settings);
    }

    [Authorize(MyERPPermissions.AccountsSettings.Edit)]
    public async Task<AccountsSettingsDto> UpdateAsync(UpdateAccountsSettingsDto input)
    {
        var settings = (await _repository.GetQueryableAsync()).FirstOrDefault();
        if (settings == null)
        {
            settings = new AccountsSettings(GuidGenerator.Create(), CurrentTenant.Id);
            ApplyChanges(settings, input);
            await _repository.InsertAsync(settings);
        }
        else
        {
            ApplyChanges(settings, input);
            await _repository.UpdateAsync(settings);
        }

        return new AccountsSettingsMapper().Map(settings);
    }

    private static void ApplyChanges(AccountsSettings entity, UpdateAccountsSettingsDto input)
    {
        entity.UnlinkPaymentOnCancellationOfInvoice = input.UnlinkPaymentOnCancellationOfInvoice;
        entity.UnlinkAdvancePaymentOnCancellationOfOrder = input.UnlinkAdvancePaymentOnCancellationOfOrder;
        entity.DeleteLinkedLedgerEntries = input.DeleteLinkedLedgerEntries;
        entity.EnableImmutableLedger = input.EnableImmutableLedger;
        entity.CheckSupplierInvoiceUniqueness = input.CheckSupplierInvoiceUniqueness;
        entity.AutomaticallyFetchPaymentTerms = input.AutomaticallyFetchPaymentTerms;
        entity.EnableSubscription = input.EnableSubscription;
        entity.EnableCommonPartyAccounting = input.EnableCommonPartyAccounting;
        entity.AllowMultiCurrencyInvoicesAgainstSinglePartyAccount = input.AllowMultiCurrencyInvoicesAgainstSinglePartyAccount;
        entity.ConfirmBeforeResettingPostingDate = input.ConfirmBeforeResettingPostingDate;
        entity.BookStockExpenseGlEntries = input.BookStockExpenseGlEntries;
        entity.EnableDiscountsAndMargin = input.EnableDiscountsAndMargin;
        entity.EnableAccountingDimensions = input.EnableAccountingDimensions;
        entity.MergeSimilarAccountHeads = input.MergeSimilarAccountHeads;
        entity.BookDeferredEntriesBasedOn = string.IsNullOrWhiteSpace(input.BookDeferredEntriesBasedOn) ? "Days" : input.BookDeferredEntriesBasedOn.Trim();
        entity.AutomaticallyProcessDeferredAccountingEntry = input.AutomaticallyProcessDeferredAccountingEntry;
        entity.BookDeferredEntriesViaJournalEntry = input.BookDeferredEntriesViaJournalEntry;
        entity.SubmitJournalEntries = input.SubmitJournalEntries;
        entity.DetermineAddressTaxCategoryFrom = string.IsNullOrWhiteSpace(input.DetermineAddressTaxCategoryFrom) ? "Billing Address" : input.DetermineAddressTaxCategoryFrom.Trim();
        entity.AddTaxesFromItemTaxTemplate = input.AddTaxesFromItemTaxTemplate;
        entity.AddTaxesFromTaxesAndChargesTemplate = input.AddTaxesFromTaxesAndChargesTemplate;
        entity.BookTaxDiscountLoss = input.BookTaxDiscountLoss;
        entity.RoundRowWiseTax = input.RoundRowWiseTax;
        entity.AllowStaleExchangeRates = input.AllowStaleExchangeRates;
        entity.StaleDays = input.StaleDays >= 0 ? input.StaleDays : 1;
        entity.AutoReconcilePayments = input.AutoReconcilePayments;
        entity.AutoReconciliationJobTrigger = input.AutoReconciliationJobTrigger > 0 ? input.AutoReconciliationJobTrigger : 15;
        entity.ReconciliationQueueSize = input.ReconciliationQueueSize > 0 ? input.ReconciliationQueueSize : 5;
        entity.OverBillingAllowance = input.OverBillingAllowance >= 0 ? input.OverBillingAllowance : 0;
        entity.CreditControllerRole = input.CreditControllerRole?.Trim();
        entity.EnableOverdueBillingThreshold = input.EnableOverdueBillingThreshold;
        entity.RoleAllowedToBypassOverdueBilling = input.RoleAllowedToBypassOverdueBilling?.Trim();
        entity.BookAssetDepreciationEntryAutomatically = input.BookAssetDepreciationEntryAutomatically;
        entity.CalculateDeprUsingTotalDays = input.CalculateDeprUsingTotalDays;
        entity.DefaultAgeingRange = string.IsNullOrWhiteSpace(input.DefaultAgeingRange) ? "30, 60, 90, 120" : input.DefaultAgeingRange.Trim();
        entity.ShowBalanceInCoa = input.ShowBalanceInCoa;
        entity.EnablePartyMatching = input.EnablePartyMatching;
        entity.EnableFuzzyMatching = input.EnableFuzzyMatching;
        entity.TransferMatchDays = input.TransferMatchDays >= 0 ? input.TransferMatchDays : 3;
        entity.CreatePrInDraftStatus = input.CreatePrInDraftStatus;
    }
}
