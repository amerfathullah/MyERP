using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Pre-submission validation for LHDN e-Invoice compliance.
/// Migrated from myinvois original.py: validate_before(), validate_before_submit(),
/// and submit_purchase.py: validate_before(), validate_before_submit().
/// Ensures documents meet LHDN requirements before submission.
/// </summary>
public class EInvoiceValidationService : ITransientDependency
{
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;

    public EInvoiceValidationService(
        IRepository<Company, Guid> companyRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository)
    {
        _companyRepository = companyRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
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

        // Invoice status validations
        if (invoice.Status != Core.DocumentStatus.Posted && invoice.Status != Core.DocumentStatus.Submitted)
            errors.Add("Invoice must be in Submitted or Posted status before e-Invoice submission.");

        if (invoice.Items.Count == 0)
            errors.Add("Invoice must have at least one line item.");

        if (invoice.GrandTotal < 0 && !invoice.IsReturn)
            errors.Add("Invoice grand total must be non-negative unless it is a return document.");

        if (string.IsNullOrWhiteSpace(invoice.BuyerTin))
        {
            var customer = await _customerRepository.FindAsync(invoice.CustomerId);
            if (string.IsNullOrWhiteSpace(customer?.Tin))
                errors.Add("Buyer TIN is required. For consumers, use generic TIN 'EI00000000020'.");
        }

        // Multi-currency conversion rate check (gotcha: no hardcoded 4.72 fallback allowed)
        if (!string.Equals(invoice.CurrencyCode, "MYR", StringComparison.OrdinalIgnoreCase) && invoice.ExchangeRate <= 0)
        {
            errors.Add("Currency conversion rate must be greater than zero for foreign currency e-Invoice.");
        }

        // Document type code validation
        var validTypeCodes = new[] { "01", "02", "03", "04", "11", "12", "13", "14" };
        var typeCode = invoice.EInvoiceDocType.HasValue ? ((int)invoice.EInvoiceDocType.Value).ToString("D2") : (invoice.IsReturn ? "02" : "01");
        if (!Array.Exists(validTypeCodes, c => c == typeCode))
            errors.Add($"Invalid document type code: {typeCode}. Must be one of: 01, 02, 03, 04, 11-14.");

        // Return / Credit Note / Debit Note validations (rules from myinvois original.py validate_before_submit)
        if (invoice.IsReturn)
        {
            if (typeCode is not "02" and not "04")
                errors.Add("Return Sales Invoice must use Document Type Code '02' (Credit Note) or '04' (Refund Note).");

            if (!invoice.ReturnAgainstId.HasValue)
                errors.Add("Credit Note / Return Invoice must reference an original invoice (ReturnAgainstId).");
            else
            {
                // LHDN requires the BillingReference to carry the original invoice's LHDN-assigned
                // UUID — submitting a Credit Note before the original has one produces a
                // non-compliant document LHDN cannot resolve back to the invoice it corrects.
                var originalInvoice = await _salesInvoiceRepository.FindAsync(invoice.ReturnAgainstId.Value);
                if (string.IsNullOrWhiteSpace(originalInvoice?.LhdnUuid))
                    errors.Add("The original invoice must have a valid LHDN submission (LhdnUuid) before its Credit Note can be submitted.");
            }
        }
        else if (typeCode is "02" or "04")
        {
            errors.Add("Credit Note or Refund Note type code can only be used on return invoices.");
        }

        // All-or-nothing tax template rule (per LHDN regulation & original.py line 857)
        var anyItemHasTax = invoice.Items.Any(i => i.TaxCategoryId.HasValue);
        var allItemsHaveTax = invoice.Items.All(i => i.TaxCategoryId.HasValue);
        if (anyItemHasTax && !allItemsHaveTax)
        {
            errors.Add("As per LHDN Regulation, if any one item has a Tax Category/Template, all items must have a Tax Category/Template.");
        }

        // Currency validation
        if (string.IsNullOrWhiteSpace(invoice.CurrencyCode))
            errors.Add("Currency code is required.");

        // Line item validations
        foreach (var item in invoice.Items)
        {
            if (item.Quantity <= 0 && !invoice.IsReturn)
                errors.Add($"Item '{item.Description}': Quantity must be greater than zero.");

            if (item.UnitPrice < 0)
                errors.Add($"Item '{item.Description}': Unit price cannot be negative.");
        }

        return errors;
    }

    /// <summary>
    /// Validate a purchase invoice (Self-billed e-Invoice) is ready for LHDN submission.
    /// Migrated from myinvois submit_purchase.py: validate_before(), validate_before_submit().
    /// </summary>
    public async Task<List<string>> ValidatePurchaseInvoiceForSubmissionAsync(PurchaseInvoice invoice, Guid companyId)
    {
        var errors = new List<string>();
        var company = await _companyRepository.GetAsync(companyId);
        var supplier = await _supplierRepository.FindAsync(invoice.SupplierId);

        // Company validations (in self-billed, Company is Buyer)
        if (string.IsNullOrWhiteSpace(company.TaxId))
            errors.Add("Company TIN (Tax Identification Number) is required for e-Invoice submission.");

        if (string.IsNullOrWhiteSpace(company.MsicCode))
            errors.Add("Company MSIC code is required for e-Invoice submission.");

        if (string.IsNullOrWhiteSpace(company.RegistrationNumber))
            errors.Add("Company registration number (BRN) is required for e-Invoice submission.");

        // Supplier validations (in self-billed, Supplier is Seller)
        var supplierTin = invoice.SupplierTin ?? supplier?.Tin;
        if (string.IsNullOrWhiteSpace(supplierTin))
            errors.Add("Supplier TIN is required for self-billed e-Invoice submission. For general public use 'EI00000000020'.");

        // Invoice status validations
        if (invoice.Status != Core.DocumentStatus.Posted && invoice.Status != Core.DocumentStatus.Submitted)
            errors.Add("Purchase Invoice must be in Submitted or Posted status before e-Invoice submission.");

        if (invoice.Items.Count == 0)
            errors.Add("Purchase Invoice must have at least one line item.");

        if (invoice.GrandTotal < 0 && !invoice.IsReturn)
            errors.Add("Purchase Invoice grand total must be non-negative unless it is a return document.");

        // Multi-currency conversion rate check
        if (!string.Equals(invoice.CurrencyCode, "MYR", StringComparison.OrdinalIgnoreCase) && invoice.ExchangeRate <= 0)
        {
            errors.Add("Currency conversion rate must be greater than zero for foreign currency e-Invoice.");
        }

        // Document type code validation for self-billed (11=Self-billed Invoice, 12=Self-billed CN, 13=Self-billed DN, 14=Self-billed Refund)
        var validSelfBilledCodes = new[] { "11", "12", "13", "14" };
        var typeCode = invoice.EInvoiceDocType.HasValue 
            ? ((int)invoice.EInvoiceDocType.Value).ToString("D2") 
            : (invoice.IsReturn ? "12" : "11");

        if (!Array.Exists(validSelfBilledCodes, c => c == typeCode))
            errors.Add($"Invalid document type code for Self-Billed Purchase Invoice: {typeCode}. Must be one of: 11, 12, 13, 14.");

        if (invoice.IsReturn)
        {
            if (typeCode is not "12" and not "14")
                errors.Add("Return Purchase Invoice must use Self-Billed Document Type Code '12' (Credit Note) or '14' (Refund Note).");

            if (!invoice.ReturnAgainstId.HasValue)
                errors.Add("Self-billed Credit Note / Return must reference an original purchase invoice (ReturnAgainstId).");
            else
            {
                var originalInvoice = await _purchaseInvoiceRepository.FindAsync(invoice.ReturnAgainstId.Value);
                if (string.IsNullOrWhiteSpace(originalInvoice?.LhdnUuid))
                    errors.Add("The original purchase invoice must have a valid LHDN submission (LhdnUuid) before its Credit Note can be submitted.");
            }
        }

        // All-or-nothing tax template rule
        var anyItemHasTax = invoice.Items.Any(i => i.TaxCategoryId.HasValue);
        var allItemsHaveTax = invoice.Items.All(i => i.TaxCategoryId.HasValue);
        if (anyItemHasTax && !allItemsHaveTax)
        {
            errors.Add("As per LHDN Regulation, if any one item has a Tax Category/Template, all items must have a Tax Category/Template.");
        }

        // Line item validations
        foreach (var item in invoice.Items)
        {
            if (item.Quantity <= 0 && !invoice.IsReturn)
                errors.Add($"Item '{item.Description}': Quantity must be greater than zero.");

            if (item.UnitPrice < 0)
                errors.Add($"Item '{item.Description}': Unit price cannot be negative.");
        }

        return errors;
    }

    /// <summary>
    /// Quick check if a sales invoice can be submitted to LHDN.
    /// </summary>
    public async Task<bool> CanSubmitAsync(SalesInvoice invoice, Guid companyId)
    {
        var errors = await ValidateForSubmissionAsync(invoice, companyId);
        return errors.Count == 0;
    }

    /// <summary>
    /// Validate and throw BusinessException if sales invoice is not valid for submission.
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

    /// <summary>
    /// Validate and throw BusinessException if purchase invoice is not valid for submission.
    /// </summary>
    public async Task EnsureValidPurchaseInvoiceForSubmissionAsync(PurchaseInvoice invoice, Guid companyId)
    {
        var errors = await ValidatePurchaseInvoiceForSubmissionAsync(invoice, companyId);
        if (errors.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EInvoiceSubmissionFailed)
                .WithData("reason", string.Join("; ", errors));
        }
    }
}
