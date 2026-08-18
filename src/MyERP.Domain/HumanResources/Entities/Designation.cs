using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Employee designation/job title master. Maps to ERPNext setup/doctype/designation.
/// Referenced by Employee.Designation via its unique Name (ERPNext autonames by designation_name).
/// </summary>
public class Designation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; set; }

    protected Designation() { }

    public Designation(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Rename(name);
        TenantId = tenantId;
    }

    public void Rename(string name)
        => Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
}
