using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Structured reason for declared lost opportunities / quotations.
/// Maps to ERPNext crm/doctype/opportunity_lost_reason.
/// </summary>
public class OpportunityLostReason : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDisabled { get; set; }

    protected OpportunityLostReason() { }

    public OpportunityLostReason(Guid id, Guid companyId, string reason, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), OpportunityLostReasonConsts.MaxReasonLength);
        TenantId = tenantId;
    }
}
