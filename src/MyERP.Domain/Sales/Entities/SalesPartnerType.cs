using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Partner Type — classification for sales partners (e.g., Reseller, Distributor, Broker, Affiliate).
/// Maps to ERPNext selling/doctype/sales_partner_type.
/// </summary>
public class SalesPartnerType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string PartnerTypeName { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected SalesPartnerType() { }

    public SalesPartnerType(Guid id, string partnerTypeName, string? description = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetPartnerTypeName(partnerTypeName);
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetPartnerTypeName(string partnerTypeName)
    {
        PartnerTypeName = Check.NotNullOrWhiteSpace(partnerTypeName, nameof(partnerTypeName), SalesPartnerTypeConsts.MaxNameLength);
    }
}
