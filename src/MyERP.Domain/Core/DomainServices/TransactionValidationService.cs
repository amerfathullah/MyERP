using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core.Entities;
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

    public TransactionValidationService(IRepository<Company, Guid> companyRepository)
    {
        _companyRepository = companyRepository;
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
}
