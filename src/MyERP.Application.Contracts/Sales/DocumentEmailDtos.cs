using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class SendStatementEmailDto
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string? RecipientEmail { get; set; }
    public string[]? CcEmails { get; set; }
    public bool AttachPdf { get; set; } = true;
}

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

public class SendSalesOrderEmailDto
{
    public Guid DocumentId { get; set; }
    public string? RecipientEmail { get; set; }
    public string[]? CcEmails { get; set; }
    public Guid? TemplateId { get; set; }
    public bool AttachPdf { get; set; } = true;
}

public class SendPurchaseOrderEmailDto
{
    public Guid DocumentId { get; set; }
    public string? RecipientEmail { get; set; }
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
