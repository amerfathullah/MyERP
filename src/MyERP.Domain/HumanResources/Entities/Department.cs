using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Department master — hierarchical org unit per company. Maps to ERPNext setup/doctype/department.
/// Referenced by Employee.Department via its unique (per company) Name.
/// </summary>
public class Department : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public Guid CompanyId { get; set; }

    /// <summary>Parent department (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>If true, this is a category group (cannot be assigned to employees directly).</summary>
    public bool IsGroup { get; set; }

    public bool IsActive { get; set; } = true;

    protected Department() { }

    public Department(Guid id, string name, Guid companyId, bool isGroup = false, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        Rename(name);
        CompanyId = companyId;
        IsGroup = isGroup;
        ParentId = parentId;
        TenantId = tenantId;
    }

    public void Rename(string name)
        => Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
}
