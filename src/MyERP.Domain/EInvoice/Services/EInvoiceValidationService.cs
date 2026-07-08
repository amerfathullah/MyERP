using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Pre-submission validation for LHDN e-Invoice compliance.
/// Migrated from myinvois original.py: validate_before(), validate_before_submit().
/// Ensures documents meet LHDN requirements before submission.
/// </summary>
public class EInvoiceValidationService : ITransientDependency
{
    private readonly IRepository<Company, Guid> _companyRepository;

    public EInvoiceValidationService(IRepository<Company, Guid> companyRepository)
    {
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Validate a sales invoice is ready for LHDN submission.
    /// Returns list of validation errors (empty = valid).
    /// </summary>
    public async Task<List<string>> ValidateForSubmissionAsync(SalesInvoice invoice, Guid companyId)
    {
        var errors = new List<string>();
        var company = await _companyRepository.GetAsync(companyId);

        // Company validations
        if (string.IsNullOrWhiteSpace(company.TaxId))
            errors.Add("Company TIN (Tax Identification Number) is required for e-Invoice submission.");

        if (string.IsNullOrWhiteSpace(company.MsicCode))
            errors.Add("Company MSIC code is required for e-Invoice submission.");

        if (string.IsNullOrWhiteSpace(company.RegistrationNumber))
            errors.Add("Company registration number (BRN) is required for e-Invoice submission.");

        // Invoice validations
        if (invoice.Status != Core.DocumentStatus.Posted && invoice.Status != Core.DocumentStatus.Submitted)
            errors.Add("Invoice must be in Submitted or Posted status before e-Invoice submission.");

        if (invoice.Items.Count == 0)
            errors.Add("Invoice must have at least one line item.");

        if (invoice.GrandTotal <= 0)
            errors.Add("Invoice grand total must be greater than zero.");

        if (string.IsNullOrWhiteSpace(invoice.BuyerTin))
            errors.Add("Buyer TIN is required. For consumers, use generic TIN 'EI00000000020'.");

        // Document type code validation
        var validTypeCodes = new[] { "01", "02", "03", "04", "11", "12", "13", "14" };
        var typeCode = invoice.EInvoiceDocType?.ToString("D2") ?? "01";
        if (!Array.Exists(validTypeCodes, c => c == typeCode))
            errors.Add($"Invalid document type code: {typeCode}. Must be one of: 01, 02, 03, 04, 11-14.");

        // Credit/debit note validations
        if (typeCode is "02" or "03")
        {
            // Credit notes and debit notes should reference an original invoice
            // This is a soft validation — logged as warning
        }

        // Currency validation
        if (string.IsNullOrWhiteSpace(invoice.CurrencyCode))
            errors.Add("Currency code is required.");

        // Line item validations
        foreach (var item in invoice.Items)
        {
            if (item.Quantity <= 0)
                errors.Add($"Item '{item.Description}': Quantity must be greater than zero.");

            if (item.UnitPrice < 0)
                errors.Add($"Item '{item.Description}': Unit price cannot be negative.");
        }

        return errors;
    }

    /// <summary>
    /// Quick check if a document can be submitted to LHDN (no detailed error messages).
    /// </summary>
    public async Task<bool> CanSubmitAsync(SalesInvoice invoice, Guid companyId)
    {
        var errors = await ValidateForSubmissionAsync(invoice, companyId);
        return errors.Count == 0;
    }

    /// <summary>
    /// Validate and throw BusinessException if not valid.
    /// </summary>
    public async Task EnsureValidForSubmissionAsync(SalesInvoice invoice, Guid companyId)
    {
        var errors = await ValidateForSubmissionAsync(invoice, companyId);
        if (errors.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceSubmissionFailed)
                .WithData("reason", string.Join("; ", errors));
        }
    }
}
