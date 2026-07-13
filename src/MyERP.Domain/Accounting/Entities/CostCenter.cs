using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Cost Center — organizational unit for cost tracking and reporting.
/// Supports tree hierarchy (group → leaf). Only leaf cost centers can receive GL postings.
/// </summary>
public class CostCenter : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;
    public string? CostCenterNumber { get; set; }

    /// <summary>Parent cost center (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>If true, this is a group node (cannot receive GL postings).</summary>
    public bool IsGroup { get; set; }

    public bool IsActive { get; set; } = true;

    protected CostCenter() { }

    public CostCenter(Guid id, Guid companyId, string name, bool isGroup = false, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        IsGroup = isGroup;
        ParentId = parentId;
        TenantId = tenantId;
    }
}
