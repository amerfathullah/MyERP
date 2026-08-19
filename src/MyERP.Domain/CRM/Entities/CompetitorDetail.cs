using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Links a Competitor to a parent document (Opportunity or Quotation).
/// Reusable across parent types via ParentType/ParentId (polymorphic), same pattern as
/// TransactionTaxRow. Maps to ERPNext crm/doctype/competitor_detail.
/// </summary>
public class CompetitorDetail : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent document type: Opportunity, Quotation.</summary>
    public string ParentType { get; set; } = null!;

    /// <summary>Parent document ID.</summary>
    public Guid ParentId { get; set; }

    public Guid CompetitorId { get; set; }

    protected CompetitorDetail() { }

    public CompetitorDetail(Guid id, string parentType, Guid parentId, Guid competitorId, Guid? tenantId = null)
        : base(id)
    {
        ParentType = parentType;
        ParentId = parentId;
        CompetitorId = competitorId;
        TenantId = tenantId;
    }
}
