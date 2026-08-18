using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Sales Stage master — configurable opportunity pipeline stage.
/// Maps to ERPNext crm/doctype/sales_stage. Referenced by Opportunity.SalesStage (stored by name).
/// </summary>
public class SalesStage : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string StageName { get; set; } = null!;

    /// <summary>Display/pipeline order.</summary>
    public int SortOrder { get; set; }

    protected SalesStage() { }

    public SalesStage(Guid id, string stageName, int sortOrder = 0, Guid? tenantId = null) : base(id)
    {
        StageName = Check.NotNullOrWhiteSpace(stageName, nameof(stageName), SalesStageConsts.MaxStageNameLength);
        SortOrder = sortOrder;
        TenantId = tenantId;
    }
}
