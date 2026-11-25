using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Validates transaction-level business rules that apply across all document types.
/// Centralized checks: posting date, company currency, frozen periods.
/// </summary>
public class TransactionValidationService : DomainService
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Project, Guid> _projectRepository;

    public TransactionValidationService(
        IRepository<Company, Guid> companyRepository,
        IRepository<Project, Guid> projectRepository)
    {
        _companyRepository = companyRepository;
        _projectRepository = projectRepository;
    }

    /// <summary>
    /// Validates a document's customer belongs to its linked project, when both are set.
    /// Per ERPNext validate_proj_cust: Project.CustomerId must be null or match the
    /// document's customer — prevents billing/costing a document against the wrong
    /// customer's project. Same rule applies to Sales Order, Delivery Note, Sales Invoice.
    /// </summary>
    public async Task ValidateProjectCustomerAsync(Guid? projectId, Guid customerId)
    {
        if (!projectId.HasValue) return;

        var project = await _projectRepository.FindAsync(projectId.Value);
        if (project == null) return;

        if (project.CustomerId.HasValue && project.CustomerId.Value != customerId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ProjectCustomerMismatch)
                .WithData("projectName", project.ProjectName)
                .WithData("customerId", customerId)
                .WithData("projectCustomerId", project.CustomerId.Value);
        }
    }

    /// <summary>
    /// Validates posting date is not in the future.
    /// Per ERPNext: future dates are only allowed with specific permission.
    /// </summary>
    public void ValidatePostingDate(DateTime postingDate)
    {
        if (postingDate.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessException(MyERPDomainErrorCodes.FuturePostingDate)
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"))
                .WithData("today", DateTime.UtcNow.Date.ToString("yyyy-MM-dd"));
        }
    }

    /// <summary>
    /// Validates that the transaction currency is valid for the company.
    /// If currency matches company's base currency, exchange rate must be 1.
    /// Per DO-NOT: cannot change company default_currency after submitted transactions exist.
    /// </summary>
    public async Task ValidateCurrencyAsync(Guid companyId, string currencyCode, decimal exchangeRate)
    {
        var company = await _companyRepository.GetAsync(companyId);

        if (string.Equals(currencyCode, company.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            // Same as base currency — exchange rate must be 1
            if (exchangeRate != 1m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.BaseCurrencyExchangeRateMustBeOne)
                    .WithData("currency", currencyCode)
                    .WithData("exchangeRate", exchangeRate);
            }
        }
        else
        {
            // Foreign currency — exchange rate must be positive
            if (exchangeRate <= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidExchangeRate)
                    .WithData("currency", currencyCode)
                    .WithData("exchangeRate", exchangeRate);
            }
        }
    }

    /// <summary>
    /// Validates that each line's rate matches its reference document's rate (within 0.01 tolerance).
    /// Per ERPNext utilities/transaction_base.py → validate_rate_with_reference_doc.
    /// Used for Buying Settings.maintain_same_rate / Selling Settings.maintain_same_sales_rate.
    /// </summary>
    /// <param name="lines">Rows to check: item description, actual rate, reference doc's rate, and reference doc type label.</param>
    /// <param name="action">"Stop" (default) blocks submission; "Warn" logs and allows it through.</param>
    /// <param name="currentUserCanOverride">True if the current user holds the configured override role — skips the Stop.</param>
    public void ValidateMaintainSameRate(
        IEnumerable<(string ItemDescription, decimal Rate, decimal ReferenceRate, string ReferenceDocType)> lines,
        string action,
        bool currentUserCanOverride)
    {
        var mismatch = lines.FirstOrDefault(l => Math.Abs(l.Rate - l.ReferenceRate) >= 0.01m);
        if (mismatch.ItemDescription == null) return;

        if (!string.Equals(action, "Stop", StringComparison.OrdinalIgnoreCase) || currentUserCanOverride)
        {
            Logger.LogWarning(
                "Row rate mismatch: item {Item} rate {Rate} vs {RefType} rate {RefRate}.",
                mismatch.ItemDescription, mismatch.Rate, mismatch.ReferenceDocType, mismatch.ReferenceRate);
            return;
        }

        throw new BusinessException(MyERPDomainErrorCodes.RateMismatchWithReferenceDoc)
            .WithData("item", mismatch.ItemDescription)
            .WithData("rate", mismatch.Rate)
            .WithData("referenceRate", mismatch.ReferenceRate)
            .WithData("referenceDocType", mismatch.ReferenceDocType);
    }

    /// <summary>
    /// Determines whether reference-document rate validation applies per ERPNext PR #69299
    /// (<c>TransactionBase.validate_rate_with_reference_doc</c>):
    /// a Quotation's rate still binds a Sales Order even when it is an internal transfer;
    /// other doctypes skip rate validation on internal transfers.
    /// </summary>
    public static bool ShouldValidateRateWithReferenceDoc(string docType, bool isInternalTransfer)
    {
        if (!isInternalTransfer)
            return true;

        return string.Equals(docType, "Sales Order", StringComparison.OrdinalIgnoreCase)
            || string.Equals(docType, "SalesOrder", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats the rate-mismatch message per ERPNext PR #69299 (<c>TransactionBase.get_rate_mismatch_message</c>):
    /// includes the numeric <paramref name="rate"/> / <paramref name="referenceRate"/> comparison only when
    /// the current user has permission on the source document holding the reference row.
    /// </summary>
    public static string FormatRateMismatchMessage(
        int rowIdx,
        string refDocType,
        string refDocName,
        decimal rate,
        decimal referenceRate,
        bool hasSourceDocPermission)
    {
        if (hasSourceDocPermission)
        {
            return $"Row #{rowIdx}: Rate must be same as {refDocType}: {refDocName} ({rate} / {referenceRate})";
        }

        return $"Row #{rowIdx}: Rate must be same as {refDocType}: {refDocName}";
    }

    /// <summary>
    /// Resolves permitted company IDs for a multi-company report (e.g. Customer Acquisition and Loyalty)
    /// per ERPNext PR #69337 (<c>customer_acquisition_and_loyalty.py: get_companies</c>):
    /// - Rejects an explicitly requested <paramref name="requestedCompanyId"/> not in <paramref name="permittedCompanyIds"/>.
    /// - Returns <c>[requestedCompanyId]</c> when selected and permitted.
    /// - Returns <paramref name="permittedCompanyIds"/> (or <c>null</c> if unrestricted) when no company is selected.
    /// </summary>
    public static IReadOnlyList<Guid>? ResolvePermittedCompaniesForReport(
        Guid? requestedCompanyId,
        IReadOnlyCollection<Guid>? permittedCompanyIds)
    {
        var hasRestriction = permittedCompanyIds != null && permittedCompanyIds.Count > 0;
        if (requestedCompanyId.HasValue && requestedCompanyId.Value != Guid.Empty)
        {
            if (hasRestriction && !permittedCompanyIds!.Contains(requestedCompanyId.Value))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("companyId", requestedCompanyId.Value)
                    .WithData("detail", $"You do not have permission to access Company: {requestedCompanyId.Value}.");
            }

            return new[] { requestedCompanyId.Value };
        }

        return hasRestriction ? permittedCompanyIds!.Distinct().ToList() : null;
    }

    /// <summary>
    /// Enforces uniform portal document refusal per ERPNext PR #67758
    /// (<c>material_request_info.py: get_permitted_doc</c>):
    /// throws the exact same <c>Not Permitted</c> error whether the document is missing,
    /// has an invalid/blank identifier, or is unreadable by the caller, preventing name enumeration.
    /// </summary>
    public static void ValidatePortalDocumentAccess(
        string? documentName,
        bool documentExists,
        bool hasReadOrWebsitePermission)
    {
        if (string.IsNullOrWhiteSpace(documentName) || !documentExists || !hasReadOrWebsitePermission)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Not Permitted");
        }
    }

    /// <summary>
    /// Enforces <c>Company</c> and <c>Mode of Payment</c> select permissions for bank/cash account lookup
    /// per ERPNext PR #68371 (<c>sales_invoice.py: get_bank_cash_account</c> /
    /// <c>journal_entry.py: get_default_bank_cash_account</c>).
    /// </summary>
    public static void ValidateCompanyAndModeOfPaymentAccess(
        Guid companyId,
        Guid? modeOfPaymentId,
        IReadOnlyCollection<Guid>? permittedCompanyIds,
        IReadOnlyCollection<Guid>? permittedModeOfPaymentIds)
    {
        if (companyId == Guid.Empty
            || (permittedCompanyIds != null && permittedCompanyIds.Count > 0 && !permittedCompanyIds.Contains(companyId)))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("companyId", companyId)
                .WithData("detail", "Not Permitted to access Company.");
        }

        if (modeOfPaymentId.HasValue
            && modeOfPaymentId.Value != Guid.Empty
            && permittedModeOfPaymentIds != null
            && permittedModeOfPaymentIds.Count > 0
            && !permittedModeOfPaymentIds.Contains(modeOfPaymentId.Value))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("modeOfPaymentId", modeOfPaymentId.Value)
                .WithData("detail", "Not Permitted to access Mode of Payment.");
        }
    }

    /// <summary>
    /// Enforces <c>Company</c> read permission before returning a company default field
    /// per ERPNext PR #68578 (<c>accounts/utils.py: get_company_default</c>),
    /// and throws when the requested default is missing unless <paramref name="ignoreValidation"/> is true.
    /// </summary>
    public static string? ResolveCompanyDefaultWithPermissionCheck(
        Guid companyId,
        string fieldName,
        string? defaultValue,
        IReadOnlyCollection<Guid>? permittedCompanyIds,
        bool ignoreValidation = false)
    {
        if (companyId == Guid.Empty
            || (permittedCompanyIds != null && permittedCompanyIds.Count > 0 && !permittedCompanyIds.Contains(companyId)))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("companyId", companyId)
                .WithData("detail", "Not Permitted to read Company.");
        }

        if (!ignoreValidation && string.IsNullOrWhiteSpace(defaultValue))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("fieldName", fieldName)
                .WithData("detail", $"Please set default {fieldName} in Company.");
        }

        return defaultValue;
    }

    /// <summary>
    /// Requires read permission on <c>Sales Order</c> before listing Sales Order calendar events
    /// per ERPNext PR #68473 (<c>sales_order.py: get_events</c>).
    /// </summary>
    public static void ValidateSalesOrderEventsReadPermission(bool hasSalesOrderReadPermission)
    {
        if (!hasSalesOrderReadPermission)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Not Permitted to read Sales Order.");
        }
    }

    /// <summary>
    /// Validates Customer Group / Supplier Group default party account rows per ERPNext PR #64596
    /// (<c>customer_group.py: validate_party_account_rows</c>):
    /// allows a row with ONLY an <c>AdvanceAccountId</c> (without requiring a Receivable/Payable <c>AccountId</c>),
    /// rejects duplicate company rows, and rejects rows where neither <c>AccountId</c> nor <c>AdvanceAccountId</c> is set.
    /// </summary>
    public static void ValidatePartyGroupAccountRows(
        IEnumerable<(Guid CompanyId, Guid? AccountId, Guid? AdvanceAccountId)>? accountRows)
    {
        if (accountRows == null)
        {
            return;
        }

        var seenCompanies = new HashSet<Guid>();
        foreach (var row in accountRows)
        {
            if (row.CompanyId == Guid.Empty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Company is required on party account row.");
            }

            if (!seenCompanies.Add(row.CompanyId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("companyId", row.CompanyId)
                    .WithData("detail", "Duplicate Company in party account rows.");
            }

            var hasRegular = row.AccountId.HasValue && row.AccountId.Value != Guid.Empty;
            var hasAdvance = row.AdvanceAccountId.HasValue && row.AdvanceAccountId.Value != Guid.Empty;
            if (!hasRegular && !hasAdvance)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("companyId", row.CompanyId)
                    .WithData("detail", "Either Account or Advance Account must be specified for each Company.");
            }
        }
    }

    /// <summary>
    /// Enforces that POS invoice consolidation state is trusted ONLY when set server-side by the POS merge log
    /// per ERPNext PR #66971 (<c>sales_invoice.py</c>, <c>pos.py</c>, <c>selling_controller.py</c>):
    /// a client-supplied <c>IsConsolidated = true</c> without <paramref name="isFromPosMergeLog"/> is reset
    /// to <c>false</c> and cannot bypass customer-block checks or enabled Mode of Payment checks.
    /// </summary>
    public static (bool EffectiveIsConsolidated, bool BypassCustomerBlockCheck, bool BypassModeOfPaymentEnabledCheck)
        ResolveTrustedPosConsolidationState(
            bool requestedIsConsolidated,
            bool isFromPosMergeLog)
    {
        var effective = requestedIsConsolidated && isFromPosMergeLog;
        return (effective, BypassCustomerBlockCheck: isFromPosMergeLog, BypassModeOfPaymentEnabledCheck: isFromPosMergeLog);
    }

    /// <summary>
    /// Builds the Payment Request Print Format list for <paramref name="refDoctype"/> respecting
    /// caller Print Format permissions per ERPNext PR #67749 (<c>payment_request.py: get_print_format_list</c>):
    /// always starts with <c>"Standard"</c> and appends only permitted formats matching <paramref name="refDoctype"/>.
    /// </summary>
    public static List<string> BuildPermittedPaymentRequestPrintFormats(
        string refDoctype,
        IEnumerable<(string Name, string DocType, bool UserHasReadPermission)>? allPrintFormats)
    {
        var list = new List<string> { "Standard" };
        if (allPrintFormats == null || string.IsNullOrWhiteSpace(refDoctype))
        {
            return list;
        }

        foreach (var pf in allPrintFormats)
        {
            if (pf.UserHasReadPermission
                && string.Equals(pf.DocType, refDoctype, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pf.Name)
                && !list.Contains(pf.Name, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(pf.Name);
            }
        }

        return list;
    }

    /// <summary>
    /// Enforces restricted default permissions for the <c>Customer</c> role on <c>Customer Group</c>
    /// per ERPNext PR #68340 (<c>customer_group.json</c> / <c>restrict_customer_group_permissions.py</c>):
    /// when <paramref name="hasReadPermission"/> is <c>false</c>, strips <c>print</c>, <c>email</c>,
    /// <c>export</c>, <c>report</c>, and <c>share</c>, retaining only <c>select</c>.
    /// </summary>
    public static (bool Select, bool Read, bool Print, bool Email, bool Export, bool Report, bool Share)
        RestrictCustomerGroupCustomerRolePermissions(
            bool select,
            bool hasReadPermission,
            bool print,
            bool email,
            bool export,
            bool report,
            bool share)
    {
        if (!hasReadPermission)
        {
            return (Select: select, Read: false, Print: false, Email: false, Export: false, Report: false, Share: false);
        }

        return (select, true, print, email, export, report, share);
    }

    /// <summary>
    /// Determines whether changing an account-currency amount on a Journal Entry row should overwrite
    /// company-currency debit/credit amounts per ERPNext PR #65261 (<c>journal_entry.js</c>):
    /// returns <c>false</c> for multi-currency <c>Exchange Gain Or Loss</c> journals because base and
    /// account-currency amounts are adjusted independently (including zero foreign balance revaluations).
    /// </summary>
    public static bool ShouldRecalculateJournalRowCompanyCurrencyAmounts(
        string? voucherType,
        bool isMultiCurrency)
    {
        return !(isMultiCurrency
            && string.Equals(voucherType, "Exchange Gain Or Loss", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Coerces nullable document totals to <c>0m</c> before taking absolute value for money-in-words
    /// formatting per ERPNext PR #68372 (<c>selling_controller.py: set_total_in_words</c>) so an empty
    /// document without items raises its normal mandatory-items error instead of a null-arithmetic crash.
    /// </summary>
    public static decimal ResolveAmountForMoneyInWords(
        decimal? grandTotal,
        decimal? roundedTotal,
        bool isRoundedTotalDisabled)
    {
        var raw = isRoundedTotalDisabled ? grandTotal : roundedTotal;
        return Math.Abs(raw ?? 0m);
    }

    /// <summary>
    /// Resolves exchange-rate sync when document currency or conversion rate changes per ERPNext PR #69369
    /// (<c>transaction.js</c>):
    /// - When <paramref name="docCurrency"/> equals <paramref name="companyCurrency"/>, forces <c>ConversionRate = 1m</c>.
    /// - When <paramref name="docCurrency"/> equals <paramref name="priceListCurrency"/>, syncs
    ///   <c>PlcConversionRate = ConversionRate</c> AND returns <c>PreservePlcConversionRate = true</c> so
    ///   <c>apply_price_list</c> keeps the user's typed rate instead of refetching the market rate.
    /// </summary>
    public static (decimal ConversionRate, decimal PlcConversionRate, bool PreservePlcConversionRate)
        ResolvePriceListRateSyncOnCurrencyChange(
            string? docCurrency,
            string? companyCurrency,
            string? priceListCurrency,
            decimal conversionRate,
            decimal currentPlcConversionRate)
    {
        var effectiveRate = string.Equals(docCurrency, companyCurrency, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : conversionRate;

        var plcFollowsCurrency = !string.IsNullOrWhiteSpace(docCurrency)
            && string.Equals(docCurrency, priceListCurrency, StringComparison.OrdinalIgnoreCase);

        var effectivePlcRate = plcFollowsCurrency ? effectiveRate : currentPlcConversionRate;
        return (effectiveRate, effectivePlcRate, PreservePlcConversionRate: plcFollowsCurrency);
    }

    /// <summary>
    /// Determines whether pricing rules must be re-applied explicitly after a transaction/posting date
    /// change per ERPNext PR #69370 (<c>transaction.js: update_rates_for_new_date</c>):
    /// a changed conversion rate already re-prices via the rate trigger; an unchanged rate fires nothing,
    /// so pricing rules must be triggered explicitly.
    /// </summary>
    public static bool ShouldReapplyPricingRulesAfterDateChange(
        decimal previousConversionRate,
        decimal currentConversionRate)
        => previousConversionRate == currentConversionRate;

    /// <summary>
    /// Keeps previously applied pricing rule IDs intact on a row UOM change (instead of clearing them to
    /// empty string) per ERPNext PR #69279 (<c>transaction.js: uom</c>) so server-side pricing rule
    /// evaluation detects rules that no longer match the new UOM and resets stale discounts.
    /// </summary>
    public static string? PreserveAppliedPricingRulesOnUomChange(string? existingAppliedPricingRules)
        => existingAppliedPricingRules;

    /// <summary>
    /// Determines whether transaction UI/print totals should show the Net Total section per ERPNext PR #68233
    /// (<c>transaction.js</c>): checks <paramref name="discountAmount"/> as a decimal (<c>!= 0m</c>) instead of
    /// truncating to int so fractional additional discounts below <c>1</c> (e.g. <c>0.50</c>) still show Net Total.
    /// </summary>
    public static bool ShouldShowNetTotalRow(decimal discountAmount, bool hasTaxIncludedInPrintRate)
        => discountAmount != 0m || hasTaxIncludedInPrintRate;

    /// <summary>
    /// Computes <c>RateWithMargin</c> for a transaction item row per ERPNext PR #67995
    /// (<c>taxes_and_totals.js: apply_pricing_rule_on_item</c>): coerces nullable <paramref name="priceListRate"/>
    /// and <paramref name="marginRateOrAmount"/> to <c>0m</c> so newly added rows with unset margin compute
    /// their rate cleanly instead of propagating null/NaN.
    /// </summary>
    public static decimal CalculateItemRateWithMargin(
        decimal? priceListRate,
        decimal? blanketOrderRate,
        decimal? conversionFactor,
        bool isSalesOrderOrQuotation,
        string? marginType,
        decimal? marginRateOrAmount,
        int precision = 4)
    {
        var effectiveRate = priceListRate ?? 0m;
        if (isSalesOrderOrQuotation && blanketOrderRate.HasValue && blanketOrderRate.Value != 0m)
        {
            var factor = conversionFactor.HasValue && conversionFactor.Value != 0m ? conversionFactor.Value : 1m;
            effectiveRate = blanketOrderRate.Value * factor;
        }

        var margin = marginRateOrAmount ?? 0m;
        var rateWithMargin = string.Equals(marginType, "Percentage", StringComparison.OrdinalIgnoreCase)
            ? effectiveRate * (1m + margin / 100m)
            : effectiveRate + margin;

        return Math.Round(rateWithMargin, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Validates that the price list is enabled (active).
    /// Per ERPNext AccountsController.validate_price_list (PR #58891 / commit f8c2f3440b):
    /// Returns retain a submitted voucher's pricing even if its price list is now disabled.
    /// In all other cases, disabled price lists are rejected.
    /// </summary>
    public async Task ValidatePriceListAsync(
        Guid? priceListId,
        bool isReturn = false,
        Guid? returnAgainstPriceListId = null)
    {
        if (!priceListId.HasValue) return;

        var priceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PriceList, Guid>>();
        var priceList = await priceListRepo.FindAsync(priceListId.Value);
        if (priceList == null) return;

        if (priceList.IsActive) return;

        // Returns retain submitted voucher's price list even if now disabled
        if (isReturn && returnAgainstPriceListId.HasValue && returnAgainstPriceListId.Value == priceListId.Value)
        {
            return;
        }

        throw new BusinessException(MyERPDomainErrorCodes.PriceListDisabled)
            .WithData("priceListName", priceList.Name);
    }
}
