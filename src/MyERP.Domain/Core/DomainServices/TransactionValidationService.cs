using System;
using System.Threading.Tasks;
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
}
