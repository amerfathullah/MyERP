using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Resolves and applies terms and conditions on draft selling and buying transactions.
/// Per ERPNext PR #62189 / commit 3a1b24cd70:
/// - Terms are auto-populated from specified tc_name (TermsAndConditionsId) or company default terms.
/// - Invariant: Company default terms are applied ONLY on draft transactions (docstatus.is_draft()).
///   Submitted, cancelled, or amended documents never have their terms overwritten.
/// </summary>
public class TermsResolutionService : DomainService
{
    private readonly IRepository<TermsAndConditions, Guid> _termsRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public TermsResolutionService(
        IRepository<TermsAndConditions, Guid> termsRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _termsRepository = termsRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Resolves selling terms for a transaction (Quotation, Sales Order, Sales Invoice).
    /// </summary>
    public async Task<(Guid? termsId, string? terms)> ResolveSellingTermsAsync(
        DocumentStatus status,
        Guid companyId,
        Guid? requestedTermsId,
        string? currentTerms)
    {
        if (status != DocumentStatus.Draft)
            return (requestedTermsId, currentTerms);

        if (!string.IsNullOrWhiteSpace(currentTerms))
            return (requestedTermsId, currentTerms);

        var termsId = requestedTermsId;
        if (!termsId.HasValue)
        {
            var company = await _companyRepository.FindAsync(companyId);
            termsId = company?.DefaultSellingTermsId;
        }

        if (termsId.HasValue)
        {
            var tc = await _termsRepository.FindAsync(termsId.Value);
            if (tc != null && !tc.IsDisabled)
            {
                return (termsId, tc.Terms);
            }
        }

        return (termsId, currentTerms);
    }

    /// <summary>
    /// Resolves buying terms for a transaction (Purchase Order, Purchase Invoice).
    /// </summary>
    public async Task<(Guid? termsId, string? terms)> ResolveBuyingTermsAsync(
        DocumentStatus status,
        Guid companyId,
        Guid? requestedTermsId,
        string? currentTerms)
    {
        if (status != DocumentStatus.Draft)
            return (requestedTermsId, currentTerms);

        if (!string.IsNullOrWhiteSpace(currentTerms))
            return (requestedTermsId, currentTerms);

        var termsId = requestedTermsId;
        if (!termsId.HasValue)
        {
            var company = await _companyRepository.FindAsync(companyId);
            termsId = company?.DefaultBuyingTermsId;
        }

        if (termsId.HasValue)
        {
            var tc = await _termsRepository.FindAsync(termsId.Value);
            if (tc != null && !tc.IsDisabled)
            {
                return (termsId, tc.Terms);
            }
        }

        return (termsId, currentTerms);
    }
}
