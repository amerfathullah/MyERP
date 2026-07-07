using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Represents a branch/location under a company.
/// Maps conceptually to ERPNext setup/doctype/branch.
/// </summary>
public class Branch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Code { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Marks this branch as the company headquarters.</summary>
    public bool IsHeadquarters { get; set; }

    protected Branch() { } // EF Core constructor

    public Branch(Guid id, Guid companyId, string name, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BranchConsts.MaxNameLength);
    }
}
