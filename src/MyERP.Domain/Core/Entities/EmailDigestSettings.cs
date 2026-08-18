using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Per-company configuration for the periodic "Email Digest" — a scheduled summary email
/// covering open sales orders, overdue invoices, and low-stock items.
/// Maps to ERPNext's Email Digest doctype (simplified to a fixed content set).
/// </summary>
public class EmailDigestSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public bool IsEnabled { get; set; }
    public EmailDigestFrequency Frequency { get; set; } = EmailDigestFrequency.Weekly;

    /// <summary>Comma-separated recipient email addresses.</summary>
    public string Recipients { get; set; } = string.Empty;

    public bool IncludeOpenSalesOrders { get; set; } = true;
    public bool IncludeOverdueInvoices { get; set; } = true;
    public bool IncludeLowStockItems { get; set; } = true;

    public DateTime? LastSentAt { get; set; }

    protected EmailDigestSettings() { }

    public EmailDigestSettings(Guid id, Guid companyId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TenantId = tenantId;
    }
}
