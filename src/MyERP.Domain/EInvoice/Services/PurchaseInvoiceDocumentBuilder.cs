using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Maps a PurchaseInvoice to EInvoiceDocumentData and uses the common InvoiceDocumentBuilder
/// to generate UBL 2.1 XML for LHDN MyInvois submission.
/// Migrated from myinvois purchase_invoice.py.
/// </summary>
public class PurchaseInvoiceDocumentBuilder : ITransientDependency
{
    private readonly InvoiceDocumentBuilder _xmlBuilder;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<MyERP.Purchasing.Entities.Supplier, Guid> _supplierRepository;

    public PurchaseInvoiceDocumentBuilder(
        InvoiceDocumentBuilder xmlBuilder,
        IRepository<Company, Guid> companyRepository,
        IRepository<MyERP.Purchasing.Entities.Supplier, Guid> supplierRepository)
    {
        _xmlBuilder = xmlBuilder;
        _companyRepository = companyRepository;
        _supplierRepository = supplierRepository;
    }

    /// <summary>
    /// Builds the UBL 2.1 XML string for a Purchase Invoice.
    /// In a Purchase Invoice, the Supplier is the Vendor and the Buyer is the Company.
    /// </summary>
    public async Task<string> BuildAsync(PurchaseInvoice invoice)
    {
        var company = await _companyRepository.GetAsync(invoice.CompanyId);
        var supplier = await _supplierRepository.GetAsync(invoice.SupplierId);

        var docType = invoice.EInvoiceDocType 
            ?? (invoice.IsReturn ? EInvoiceDocumentType.SelfBilledCreditNote : EInvoiceDocumentType.SelfBilledInvoice);

        var data = new EInvoiceDocumentData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DocumentTypeCode = ((int)docType).ToString("D2"),
            CurrencyCode = invoice.CurrencyCode,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            DiscountAmount = invoice.DiscountAmount,
            
            // For Purchase Invoices:
            // Supplier = The Vendor
            Supplier = new EInvoicePartyData
            {
                Name = supplier.Name,
                Tin = invoice.SupplierTin ?? supplier.Tin ?? "EI00000000020",
                IdType = supplier.IdType ?? "BRN",
                IdValue = supplier.IdValue ?? supplier.RegistrationNumber ?? "NA",
                SstRegistration = supplier.SstRegistrationNumber,
                Address = supplier.Address ?? "NA",
                City = supplier.City,
                State = supplier.State,
                PostalCode = supplier.PostalCode,
                CountryCode = supplier.Country ?? "MYS"
            },

            // Buyer = The Company
            Buyer = new EInvoicePartyData
            {
                Name = company.Name,
                Tin = invoice.BuyerTin ?? company.TaxId ?? "NA",
                IdType = "BRN",
                IdValue = company.RegistrationNumber ?? "NA",
                MsicCode = company.MsicCode,
                SstRegistration = company.SstRegistrationNumber ?? "NA",
                Address = company.Address ?? "NA",
                City = company.City,
                State = company.State,
                PostalCode = company.PostalCode,
                CountryCode = company.Country ?? "MYS"
            }
        };

        // If this is a self-billed return (Debit/Credit note), LHDN requires original reference
        if ((data.DocumentTypeCode is "02" or "03" or "04" or "12" or "13" or "14") 
            && invoice.ReturnAgainstId.HasValue)
        {
            data.BillingReferenceNumber = invoice.ReturnAgainstId.Value.ToString();
        }

        // Map Lines
        foreach (var item in invoice.Items)
        {
            data.Lines.Add(new EInvoiceLineData
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxAmount = item.TaxAmount,
                Uom = item.Uom,
                TaxCategoryCode = "01", // Standard rate
                TaxRate = 0m
            });
        }

        // Generate the XML using the common builder
        return _xmlBuilder.Build(data);
    }
}
