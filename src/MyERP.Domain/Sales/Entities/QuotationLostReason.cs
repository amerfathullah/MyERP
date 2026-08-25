using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Quotation Lost Reason — explains why a quotation or sales opportunity was lost / declined.
/// Maps to ERPNext setup/doctype/quotation_lost_reason.
/// </summary>
public class QuotationLostReason : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected QuotationLostReason() { }

    public QuotationLostReason(Guid id, string reason, string? description = null, Guid? tenantId = null)
        : base(id)
    {
        Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: QuotationLostReasonConsts.MaxReasonLength);
        Description = description;
        TenantId = tenantId;
    }
}
