using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
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

    /// <summary>Sales targets for this territory per fiscal year and item group.</summary>
    public ICollection<TerritoryTarget> Targets { get; private set; } = new List<TerritoryTarget>();

    protected Territory() { }

    public Territory(Guid id, string name, Guid? parentId = null, bool isGroup = false, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ParentId = parentId;
        IsGroup = isGroup;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a territory sales target.
    /// Per ERPNext / Target Detail: targets cannot be negative, cannot duplicate (fiscalYear, itemGroup), and must not both be zero.
    /// </summary>
    public void AddTarget(Guid? fiscalYearId, decimal targetQty, decimal targetAmount, Guid? itemGroupId = null, Guid? distributionId = null)
    {
        if (targetQty < 0 || targetAmount < 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Target Qty and Target Amount cannot be negative.");

        if (targetQty == 0 && targetAmount == 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Either target qty or target amount is mandatory.");

        if (Targets.Any(t => t.FiscalYearId == fiscalYearId && t.ItemGroupId == itemGroupId))
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "A target already exists for this fiscal year and item group.");

        Targets.Add(new TerritoryTarget(
            Guid.NewGuid(), Id, fiscalYearId, targetQty, targetAmount, itemGroupId, distributionId));
    }

    public void RemoveTarget(Guid targetId)
    {
        var target = Targets.FirstOrDefault(t => t.Id == targetId);
        if (target != null)
        {
            Targets.Remove(target);
        }
    }
}

/// <summary>
/// Sales target for a territory (qty or amount target per fiscal year and item group).
/// Maps to ERPNext Target Detail child table on Territory.
/// </summary>
public class TerritoryTarget : Entity<Guid>
{
    public Guid TerritoryId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? DistributionId { get; set; }

    protected TerritoryTarget() { }

    public TerritoryTarget(
        Guid id,
        Guid territoryId,
        Guid? fiscalYearId,
        decimal targetQty,
        decimal targetAmount,
        Guid? itemGroupId = null,
        Guid? distributionId = null)
        : base(id)
    {
        TerritoryId = territoryId;
        FiscalYearId = fiscalYearId;
        TargetQty = targetQty;
        TargetAmount = targetAmount;
        ItemGroupId = itemGroupId;
        DistributionId = distributionId;
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
