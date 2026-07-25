using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Child entity recording which companies a restricted master (Item/Customer/Supplier) is allowed for.
/// Per ERPNext PR #57258+#57352: each master optionally restricts visibility + transaction usage by company.
/// </summary>
public class CompanyRestrictionEntry : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>The parent document type: "Item", "Customer", or "Supplier".</summary>
    public string ParentType { get; private set; } = null!;

    /// <summary>The parent document ID.</summary>
    public Guid ParentId { get; private set; }

    /// <summary>The allowed company ID.</summary>
    public Guid CompanyId { get; private set; }

    protected CompanyRestrictionEntry() { }

    public CompanyRestrictionEntry(Guid id, string parentType, Guid parentId, Guid companyId, Guid? tenantId = null)
        : base(id)
    {
        ParentType = parentType;
        ParentId = parentId;
        CompanyId = companyId;
        TenantId = tenantId;
    }
}
