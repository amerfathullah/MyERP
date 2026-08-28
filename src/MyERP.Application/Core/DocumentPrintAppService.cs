using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Document Print App Service — generates printable HTML/PDF content for documents.
/// Called by Angular when user clicks "Download PDF" or "Print" on document detail pages.
/// Returns HTML content that can be rendered in a new window or converted to PDF client-side.
/// 
/// Per ERPNext: /api/method/frappe.utils.print_format.download_pdf
/// </summary>
[Authorize]
public class DocumentPrintAppService : ApplicationService, IDocumentPrintAppService
{
    private readonly IDocumentPdfService _pdfService;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public DocumentPrintAppService(
        IDocumentPdfService pdfService,
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _pdfService = pdfService;
        _siRepository = siRepository;
        _poRepository = poRepository;
        _quotationRepository = quotationRepository;
        _dnRepository = dnRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Generate printable HTML for a Sales Invoice.
    /// Returns HTML string that Angular renders in a print window.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Default)]
    public async Task<DocumentPrintResult> GetSalesInvoicePrintAsync(Guid invoiceId)
    {
        var invoice = await _siRepository.GetAsync(invoiceId);
        var customer = await _customerRepository.GetAsync(invoice.CustomerId);
        var company = await _companyRepository.GetAsync(invoice.CompanyId);

        var data = new SalesInvoicePdfData
        {
            CompanyName = company.Name,
            CompanyTin = company.TaxId,
            CompanySst = company.SstRegistrationNumber,
            CompanyAddress = company.Address,
            CompanyPhone = company.Phone,
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            IsReturn = invoice.IsReturn,
            CustomerName = customer.Name,
            CustomerTin = customer.Tin,
            CustomerAddress = customer.Address,
            Currency = invoice.CurrencyCode,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = invoice.DiscountAmount,
            GrandTotal = invoice.GrandTotal,
            Notes = invoice.Notes,
            Items = invoice.Items.Select(i => new PdfLineItem
            {
                Description = i.Description,
                Quantity = i.Quantity,
                Rate = i.UnitPrice,
            }).ToList(),
        };

        var pdfBytes = await _pdfService.GenerateSalesInvoicePdfAsync(data);
        return new DocumentPrintResult
        {
            PdfBytes = pdfBytes,
            FileName = $"{invoice.InvoiceNumber}.pdf",
            DocumentType = invoice.IsReturn ? "Credit Note" : "Tax Invoice",
        };
    }

    /// <summary>
    /// Generate printable HTML for a Purchase Order.
    /// </summary>
    [Authorize(MyERPPermissions.PurchaseOrders.Default)]
    public async Task<DocumentPrintResult> GetPurchaseOrderPrintAsync(Guid orderId)
    {
        var po = await _poRepository.GetAsync(orderId);
        var supplier = await _supplierRepository.GetAsync(po.SupplierId);
        var company = await _companyRepository.GetAsync(po.CompanyId);

        var data = new PurchaseOrderPdfData
        {
            CompanyName = company.Name,
            CompanyAddress = company.Address,
            OrderNumber = po.OrderNumber,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            SupplierName = supplier.Name,
            SupplierAddress = supplier.Address,
            Currency = po.CurrencyCode,
            GrandTotal = po.GrandTotal,
            Terms = po.Terms,
            Items = po.Items.Select(i => new PdfLineItem
            {
                Description = i.Description,
                Quantity = i.Quantity,
                Rate = i.UnitPrice,
            }).ToList(),
        };

        var pdfBytes = await _pdfService.GeneratePurchaseOrderPdfAsync(data);
        return new DocumentPrintResult
        {
            PdfBytes = pdfBytes,
            FileName = $"{po.OrderNumber}.pdf",
            DocumentType = "Purchase Order",
        };
    }

    /// <summary>
    /// Generate printable HTML for a Quotation.
    /// </summary>
    [Authorize(MyERPPermissions.Quotations.Default)]
    public async Task<DocumentPrintResult> GetQuotationPrintAsync(Guid quotationId)
    {
        var quotation = await _quotationRepository.GetAsync(quotationId);
        var company = await _companyRepository.GetAsync(quotation.CompanyId);

        // Resolve party name
        string partyName = "";
        var customer = await _customerRepository.FindAsync(quotation.CustomerId);
        partyName = customer?.Name ?? "";

        var data = new QuotationPdfData
        {
            CompanyName = company.Name,
            CompanyAddress = company.Address,
            QuotationNumber = quotation.QuotationNumber ?? "",
            TransactionDate = quotation.IssueDate,
            ValidTill = quotation.ValidUntil,
            PartyName = partyName,
            Currency = quotation.CurrencyCode,
            NetTotal = quotation.NetTotal,
            TaxAmount = quotation.TaxAmount,
            GrandTotal = quotation.GrandTotal,
            Terms = quotation.Terms,
            Items = quotation.Items.Select(i => new PdfLineItem
            {
                Description = i.Description,
                Quantity = i.Quantity,
                Rate = i.UnitPrice,
            }).ToList(),
        };

        var pdfBytes = await _pdfService.GenerateQuotationPdfAsync(data);
        return new DocumentPrintResult
        {
            PdfBytes = pdfBytes,
            FileName = $"{quotation.QuotationNumber ?? "Quotation"}.pdf",
            DocumentType = "Quotation",
        };
    }

    /// <summary>
    /// Generate printable HTML for a Delivery Note.
    /// </summary>
    [Authorize(MyERPPermissions.DeliveryNotes.Default)]
    public async Task<DocumentPrintResult> GetDeliveryNotePrintAsync(Guid deliveryNoteId)
    {
        var dn = await _dnRepository.GetAsync(deliveryNoteId);
        var customer = await _customerRepository.GetAsync(dn.CustomerId);
        var company = await _companyRepository.GetAsync(dn.CompanyId);

        var data = new DeliveryNotePdfData
        {
            CompanyName = company.Name,
            DeliveryNumber = dn.DeliveryNumber,
            PostingDate = dn.PostingDate,
            CustomerName = customer.Name,
            ShippingAddress = dn.ShippingAddress,
            TransporterInfo = dn.Transporter,
            Items = dn.Items.Select(i => new DeliveryNoteLineItem
            {
                Description = i.Description,
                Quantity = i.Quantity,
                Uom = i.Uom ?? "Unit",
            }).ToList(),
        };

        var pdfBytes = await _pdfService.GenerateDeliveryNotePdfAsync(data);
        return new DocumentPrintResult
        {
            PdfBytes = pdfBytes,
            FileName = $"{dn.DeliveryNumber}.pdf",
            DocumentType = "Delivery Note",
        };
    }
}

