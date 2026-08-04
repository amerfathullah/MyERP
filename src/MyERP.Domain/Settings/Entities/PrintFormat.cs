using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Settings.Entities;

/// <summary>
/// Print Format — defines HTML/Razor layout for printing documents like Sales Invoices.
/// </summary>
public class PrintFormat : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Name of the print format (e.g. "Standard Sales Invoice").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Document type it applies to (e.g. "SalesInvoice").</summary>
    public string DocumentType { get; set; } = null!;

    /// <summary>Is this the default print format for the Document Type?</summary>
    public bool IsDefault { get; set; }

    /// <summary>Custom HTML/Razor template body.</summary>
    public string HtmlTemplate { get; set; } = null!;

    public PrintFormatType FormatType { get; set; } = PrintFormatType.Custom;
    public string? FormatData { get; set; }

    /// <summary>Custom CSS for styling.</summary>
    public string? CssStyles { get; set; }
    
    public string? HeaderHtml { get; set; }
    public string? FooterHtml { get; set; }

    protected PrintFormat() { }

    public PrintFormat(Guid id, Guid companyId, string name, string documentType, bool isDefault, string htmlTemplate, PrintFormatType formatType = PrintFormatType.Custom, string? formatData = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = name;
        DocumentType = documentType;
        IsDefault = isDefault;
        HtmlTemplate = htmlTemplate;
        FormatType = formatType;
        FormatData = formatData;
        TenantId = tenantId;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    public void UpdateTemplate(string htmlTemplate, string? cssStyles = null, string? headerHtml = null, string? footerHtml = null, PrintFormatType? formatType = null, string? formatData = null)
    {
        HtmlTemplate = htmlTemplate;
        CssStyles = cssStyles;
        HeaderHtml = headerHtml;
        FooterHtml = footerHtml;
        
        if (formatType.HasValue) FormatType = formatType.Value;
        if (formatData != null) FormatData = formatData;
    }
}
