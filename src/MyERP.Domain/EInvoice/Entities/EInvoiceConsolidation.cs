using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.EInvoice.Entities;

/// <summary>
/// Tracks the mapping of original B2C Sales Invoices to their LHDN Consolidated e-Invoice.
/// Migrated from myinvois consolidate_invoice.py (custom_consolidate_invoice_number).
/// </summary>
public class EInvoiceConsolidation : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>
    /// The consolidated Sales Invoice ID that is submitted to LHDN.
    /// </summary>
    public Guid ConsolidatedInvoiceId { get; set; }

    /// <summary>
    /// The original (B2C) Sales Invoice ID.
    /// </summary>
    public Guid OriginalInvoiceId { get; set; }

    protected EInvoiceConsolidation() { }

    public EInvoiceConsolidation(Guid id, Guid companyId, Guid consolidatedInvoiceId, Guid originalInvoiceId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ConsolidatedInvoiceId = consolidatedInvoiceId;
        OriginalInvoiceId = originalInvoiceId;
        TenantId = tenantId;
    }
}
