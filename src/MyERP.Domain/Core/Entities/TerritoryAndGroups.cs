using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Territory — hierarchical sales territory for customer assignment, 
/// tax rule matching, pricing rule scoping, and SLA resolution.
/// Uses tree structure (parent/child) like ItemGroup and CostCenter.
/// 
/// Per ERPNext: Territory uses NestedSet (for shipping rules, tax rules, SLA matching).
/// Default territories: "All Territories" (root) → country → "Rest Of The World"
/// </summary>
public class Territory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Parent territory (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>True if this is a group node (cannot be assigned to customers directly).</summary>
    public bool IsGroup { get; set; }

    /// <summary>Territory manager (employee) for this region.</summary>
    public Guid? TerritoryManagerId { get; set; }

    protected Territory() { }

    public Territory(Guid id, string name, Guid? parentId = null, bool isGroup = false, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ParentId = parentId;
        IsGroup = isGroup;
        TenantId = tenantId;
    }
}

/// <summary>
/// Customer Group — hierarchical categorization for customers.
/// Used in: credit limit defaults, pricing rules, tax rules, reporting.
/// 
/// Per ERPNext: Customers can only be assigned to leaf nodes (is_group=false).
/// Default: "All Customer Groups" (root) + country-specific leaf nodes.
/// </summary>
public class CustomerGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Parent group (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>True if this is a group node (cannot be assigned to customers directly).</summary>
    public bool IsGroup { get; set; }

    /// <summary>Default payment terms for customers in this group.</summary>
    public Guid? DefaultPaymentTermsTemplateId { get; set; }

    /// <summary>Default price list for this customer group.</summary>
    public Guid? DefaultPriceListId { get; set; }

    /// <summary>Default credit limit for new customers in this group.</summary>
    public decimal DefaultCreditLimit { get; set; }

    protected CustomerGroup() { }

    public CustomerGroup(Guid id, string name, Guid? parentId = null, bool isGroup = false, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ParentId = parentId;
        IsGroup = isGroup;
        TenantId = tenantId;
    }
}

/// <summary>
/// Supplier Group — hierarchical categorization for suppliers.
/// Used in: default payment terms, reporting, supplier scorecard grouping.
/// 
/// Per ERPNext: "All Supplier Groups" (root) → leaf nodes.
/// </summary>
public class SupplierGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Parent group (null = root).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>True if this is a group node.</summary>
    public bool IsGroup { get; set; }

    /// <summary>Default payment terms for suppliers in this group.</summary>
    public Guid? DefaultPaymentTermsTemplateId { get; set; }

    protected SupplierGroup() { }

    public SupplierGroup(Guid id, string name, Guid? parentId = null, bool isGroup = false, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ParentId = parentId;
        IsGroup = isGroup;
        TenantId = tenantId;
    }
}
