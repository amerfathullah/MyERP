using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Child table linking a Quotation to a QuotationLostReason.
/// Maps to ERPNext setup/doctype/quotation_lost_reason_detail.
/// </summary>
public class QuotationLostReasonDetail : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid QuotationId { get; set; }
    public Guid QuotationLostReasonId { get; set; }

    protected QuotationLostReasonDetail() { }

    public QuotationLostReasonDetail(Guid id, Guid quotationId, Guid quotationLostReasonId, Guid? tenantId = null)
        : base(id)
    {
        QuotationId = quotationId;
        QuotationLostReasonId = quotationLostReasonId;
        TenantId = tenantId;
    }
}
