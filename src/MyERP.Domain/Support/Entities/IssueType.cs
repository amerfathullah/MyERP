using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Support.Entities;

/// <summary>Configurable Issue type master (e.g. Bug, Feature Request, Complaint). Maps to ERPNext Issue Type.</summary>
public class IssueType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    protected IssueType() { }

    public IssueType(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), IssueTypeConsts.MaxNameLength);
        TenantId = tenantId;
    }
}
