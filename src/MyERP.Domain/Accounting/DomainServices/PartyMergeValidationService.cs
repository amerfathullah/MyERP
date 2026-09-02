using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Validates party currency consistency before merging parties (per ERPNext PR #51171 / commit f48b90c600).
/// Disallows merging if old and new parties have existing accounting entries in different currencies for the same company.
/// </summary>
public class PartyMergeValidationService : DomainService
{
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PartyMergeValidationService(
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _pleRepository = pleRepository;
        _companyRepository = companyRepository;
    }

    public async Task ValidatePartyCurrencyBeforeMergingAsync(string partyType, Guid oldPartyId, Guid newPartyId, string oldPartyName, string newPartyName)
    {
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var companies = await _companyRepository.GetListAsync();

        foreach (var company in companies)
        {
            var oldPartyCurrency = pleQuery
                .Where(p => p.CompanyId == company.Id && p.PartyType == partyType && p.PartyId == oldPartyId && !p.Delinked)
                .Select(p => p.AccountCurrency)
                .FirstOrDefault();

            var newPartyCurrency = pleQuery
                .Where(p => p.CompanyId == company.Id && p.PartyType == partyType && p.PartyId == newPartyId && !p.Delinked)
                .Select(p => p.AccountCurrency)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(oldPartyCurrency) && !string.IsNullOrEmpty(newPartyCurrency)
                && !string.Equals(oldPartyCurrency, newPartyCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot merge {partyType} '{oldPartyName}' into '{newPartyName}' as both have existing accounting entries in different currencies for company '{company.Name}'.");
            }
        }
    }
}
