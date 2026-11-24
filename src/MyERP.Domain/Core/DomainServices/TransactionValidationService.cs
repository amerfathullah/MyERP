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
