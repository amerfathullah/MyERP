using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Industry classification master. Maps to ERPNext selling/doctype/industry_type.
/// Referenced by Lead.Industry via its unique Name (ERPNext autonames by industry).
/// </summary>
public class IndustryType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    protected IndustryType() { }

    public IndustryType(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), IndustryTypeConsts.MaxNameLength);
        TenantId = tenantId;
    }
}
