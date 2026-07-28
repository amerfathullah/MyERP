using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Handles sending document emails from detail pages (Send Email action).
/// Covers: Sales Invoice, Quotation, Delivery Note email dispatch.
/// Per ERPNext: "Make" → "Email" button on submitted document detail views.
/// </summary>
[Authorize]
public class DocumentEmailAppService : ApplicationService
{
    private readonly DocumentEmailService _emailService;
    private readonly IDocumentPdfService _pdfService;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public DocumentEmailAppService(
        DocumentEmailService emailService,
        IDocumentPdfService pdfService,
        IRepository<SalesInvoice, Guid> siRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _emailService = emailService;
        _pdfService = pdfService;
        _siRepository = siRepository;
        _quotationRepository = quotationRepository;
        _dnRepository = dnRepository;
        _customerRepository = customerRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Send a Sales Invoice email to the customer with optional PDF attachment.
    /// </summary>
    [Authorize(MyERPPermissions.SalesInvoices.Default)]
    public async Task SendSalesInvoiceEmailAsync(SendInvoiceEmailDto input)
    {
        var invoice = await _siRepository.GetAsync(input.InvoiceId);
        var customer = await _customerRepository.GetAsync(invoice.CustomerId);
        var company = await _companyRepository.GetAsync(invoice.CompanyId);

        var recipientEmail = input.RecipientEmail ?? customer.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new Volo.Abp.BusinessException("MyERP:09001")
                .WithData("reason", "No email address found for customer. Please provide a recipient email.");

        var variables = new Dictionary<string, string>
        {
            ["company_name"] = company.Name,
            ["customer_name"] = customer.Name,
            ["party_name"] = customer.Name,
            ["invoice_number"] = invoice.InvoiceNumber,
            ["document_number"] = invoice.InvoiceNumber,
            ["issue_date"] = invoice.IssueDate.ToString("dd/MM/yyyy"),
            ["grand_total"] = $"{invoice.CurrencyCode} {invoice.GrandTotal:N2}",
            ["due_date"] = invoice.DueDate?.ToString("dd/MM/yyyy") ?? "",
        };

        SalesInvoicePdfData? pdfData = null;
        if (input.AttachPdf)
        {
            pdfData = new SalesInvoicePdfData
            {
                CompanyName = company.Name,
                CompanyTin = company.TaxId,
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
        }

        await _emailService.SendSalesInvoiceEmailAsync(new SendDocumentEmailInput
        {
            RecipientEmail = recipientEmail,
            CcEmails = input.CcEmails,
            TemplateId = input.TemplateId,
            Variables = variables,
            AttachPdf = input.AttachPdf,
            PdfData = pdfData,
        });
    }

    /// <summary>
    /// Send a Quotation email to the customer/prospect.
    /// </summary>
    [Authorize(MyERPPermissions.Quotations.Default)]
    public async Task SendQuotationEmailAsync(SendQuotationEmailDto input)
    {
        var quotation = await _quotationRepository.GetAsync(input.QuotationId);
        var company = await _companyRepository.GetAsync(quotation.CompanyId);

        var recipientEmail = input.RecipientEmail;
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new Volo.Abp.BusinessException("MyERP:09001")
                .WithData("reason", "Recipient email is required for quotation.");

        var variables = new Dictionary<string, string>
        {
            ["company_name"] = company.Name,
            ["party_name"] = input.PartyName ?? "",
            ["quotation_number"] = quotation.QuotationNumber ?? "",
            ["document_number"] = quotation.QuotationNumber ?? "",
            ["transaction_date"] = quotation.IssueDate.ToString("dd/MM/yyyy"),
            ["grand_total"] = $"{quotation.CurrencyCode} {quotation.GrandTotal:N2}",
            ["valid_till"] = quotation.ValidUntil?.ToString("dd/MM/yyyy") ?? "",
        };

        QuotationPdfData? pdfData = null;
        if (input.AttachPdf)
        {
            pdfData = new QuotationPdfData
            {
                CompanyName = company.Name,
                CompanyAddress = company.Address,
                QuotationNumber = quotation.QuotationNumber ?? "",
                TransactionDate = quotation.IssueDate,
                ValidTill = quotation.ValidUntil,
                PartyName = input.PartyName ?? "",
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
        }

        await _emailService.SendQuotationEmailAsync(new SendQuotationEmailInput
        {
            RecipientEmail = recipientEmail,
            CcEmails = input.CcEmails,
            TemplateId = input.TemplateId,
            Variables = variables,
            AttachPdf = input.AttachPdf,
            PdfData = pdfData,
        });
    }

    /// <summary>
    /// Get a preview of the email that would be sent (subject + body with variables substituted).
    /// </summary>
    public async Task<EmailPreviewDto> PreviewEmailAsync(PreviewEmailInput input)
    {
        var templateRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<EmailTemplate, Guid>>();
        var query = await templateRepo.GetQueryableAsync();

        EmailTemplate? template;
        if (input.TemplateId.HasValue)
        {
            template = await templateRepo.FindAsync(input.TemplateId.Value);
        }
        else
        {
            template = query
                .Where(t => t.DocumentType == input.DocumentType && t.IsEnabled)
                .OrderByDescending(t => t.CreationTime)
                .FirstOrDefault();
        }

        if (template == null)
        {
            return new EmailPreviewDto
            {
                Subject = $"{input.Variables.GetValueOrDefault("company_name", "")} - {input.DocumentType} {input.Variables.GetValueOrDefault("document_number", "")}",
                Body = $"<p>Dear {input.Variables.GetValueOrDefault("party_name", "Customer")},</p><p>Please find attached.</p>",
            };
        }

        var subject = template.Subject;
        var body = template.Body;

        foreach (var kvp in input.Variables)
        {
            subject = subject.Replace($"{{{{ {kvp.Key} }}}}", kvp.Value);
            subject = subject.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            body = body.Replace($"{{{{ {kvp.Key} }}}}", kvp.Value);
            body = body.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }

        return new EmailPreviewDto { Subject = subject, Body = body };
    }
}

// DTOs
public class SendInvoiceEmailDto
{
    public Guid InvoiceId { get; set; }
    public string? RecipientEmail { get; set; }
    public string[]? CcEmails { get; set; }
    public Guid? TemplateId { get; set; }
    public bool AttachPdf { get; set; } = true;
}

public class SendQuotationEmailDto
{
    public Guid QuotationId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? PartyName { get; set; }
    public string[]? CcEmails { get; set; }
    public Guid? TemplateId { get; set; }
    public bool AttachPdf { get; set; } = true;
}

public class PreviewEmailInput
{
    public string DocumentType { get; set; } = "";
    public Guid? TemplateId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class EmailPreviewDto
{
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}
