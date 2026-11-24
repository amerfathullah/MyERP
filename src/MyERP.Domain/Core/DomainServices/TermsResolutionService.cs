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

    /// <summary>
    /// Returns <c>"selling"</c> or <c>"buying"</c> for documents that enforce side-specific
    /// Terms and Conditions templates per ERPNext PR #65671 (<c>terms_and_conditions.py: get_terms_module</c>).
    /// </summary>
    public static string? GetTermsModule(string? docType)
    {
        if (string.IsNullOrWhiteSpace(docType))
        {
            return null;
        }

        return docType.Replace(" ", string.Empty).ToUpperInvariant() switch
        {
            "QUOTATION" or "SALESORDER" or "DELIVERYNOTE" or "SALESINVOICE" or "POSINVOICE" or "POSPROFILE" => "selling",
            "SUPPLIERQUOTATION" or "PURCHASEORDER" or "PURCHASERECEIPT" or "PURCHASEINVOICE"
                or "REQUESTFORQUOTATION" or "SUBCONTRACTINGORDER" or "SUBCONTRACTINGRECEIPT" => "buying",
            _ => null
        };
    }

    /// <summary>
    /// Clears a mapped Terms and Conditions template and rendered terms when the template is not
    /// enabled for the target document's selling/buying side per ERPNext PR #65671
    /// (<c>terms_and_conditions.py: clear_inapplicable_terms</c>).
    /// </summary>
    public static (Guid? TermsId, string? TermsText) ClearInapplicableMappedTerms(
        string? targetDocType,
        Guid? termsId,
        string? termsText,
        bool templateIsSelling,
        bool templateIsBuying)
    {
        if (!termsId.HasValue || termsId.Value == Guid.Empty)
        {
            return (termsId, termsText);
        }

        var module = GetTermsModule(targetDocType);
        if (module == "selling" && !templateIsSelling)
        {
            return (null, null);
        }

        if (module == "buying" && !templateIsBuying)
        {
            return (null, null);
        }

        return (termsId, termsText);
    }
}
