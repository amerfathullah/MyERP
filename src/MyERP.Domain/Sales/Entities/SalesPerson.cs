using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Person — hierarchical sales team member for commission tracking and territory management.
/// Uses tree structure (parent/child) like Territory and CustomerGroup.
/// 
/// Per ERPNext:
/// - One Employee can only be linked to ONE Sales Person
/// - Cannot disable if linked to any Customer
/// - Auto-parent: defaults to root "All Sales Persons" if no parent set
/// - Commission allocated based on allocated_percentage × commission_rate
/// - Email resolved via: SalesPerson → Employee → User → email
/// 
/// Source: erpnext/setup/doctype/sales_person/sales_person.py
/// </summary>
public class SalesPerson : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Sales person name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Parent in the sales hierarchy (null = root).</summary>
    public Guid? ParentSalesPersonId { get; set; }

    /// <summary>True if this is a group/manager node (not directly assigned to transactions).</summary>
    public bool IsGroup { get; set; }

    /// <summary>Linked employee (one employee → one sales person only).</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>Commission rate for this sales person (percentage, e.g., 5 = 5%).</summary>
    public decimal CommissionRate { get; set; }

    /// <summary>Whether this sales person is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Sales targets for this person.</summary>
    public ICollection<SalesPersonTarget> Targets { get; private set; }
        = new List<SalesPersonTarget>();

    protected SalesPerson() { }

    public SalesPerson(Guid id, string name, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        ParentSalesPersonId = parentId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Set the commission rate.
    /// </summary>
    public void SetCommissionRate(decimal rate)
    {
        if (rate < 0 || rate > 100)
            throw new BusinessException("MyERP:03012")
                .WithData("rate", rate);
        CommissionRate = rate;
    }

    /// <summary>
    /// Disable this sales person.
    /// Per ERPNext: cannot disable if linked to customers (checked at service level).
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
    }

    /// <summary>
    /// Add a sales target.
    /// Per ERPNext: must have either qty or amount (not both zero).
    /// </summary>
    public void AddTarget(Guid? fiscalYearId, decimal targetQty, decimal targetAmount, Guid? itemGroupId = null)
    {
        if (targetQty == 0 && targetAmount == 0)
            throw new BusinessException("MyERP:03013")
                .WithData("salesPerson", Name);

        Targets.Add(new SalesPersonTarget(
            Guid.NewGuid(), Id, fiscalYearId, targetQty, targetAmount, itemGroupId));
    }

    /// <summary>
    /// Calculate commission for an allocated amount.
    /// incentive = allocated_amount × commission_rate / 100
    /// </summary>
    public decimal CalculateCommission(decimal allocatedAmount)
    {
        return Math.Round(allocatedAmount * CommissionRate / 100m, 2);
    }
}

/// <summary>
/// Sales target for a sales person (qty or amount target per fiscal year).
/// </summary>
public class SalesPersonTarget : Entity<Guid>
{
    public Guid SalesPersonId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
    public Guid? ItemGroupId { get; set; }

    protected SalesPersonTarget() { }

    public SalesPersonTarget(Guid id, Guid salesPersonId, Guid? fiscalYearId,
        decimal targetQty, decimal targetAmount, Guid? itemGroupId) : base(id)
    {
        SalesPersonId = salesPersonId;
        FiscalYearId = fiscalYearId;
        TargetQty = targetQty;
        TargetAmount = targetAmount;
        ItemGroupId = itemGroupId;
    }
}

/// <summary>
/// Links a sales person to a transaction (Sales Order, Sales Invoice, etc.)
/// with their allocated percentage and calculated commission.
/// Embedded as a child row in selling documents.
/// </summary>
public class SalesTeamEntry : Entity<Guid>
{
    public Guid SalesPersonId { get; set; }

    /// <summary>Percentage of the transaction allocated to this sales person.</summary>
    public decimal AllocatedPercentage { get; set; }

    /// <summary>Calculated: eligible_amount × allocated_percentage / 100.</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>Commission rate at time of transaction (snapshot from SalesPerson).</summary>
    public decimal CommissionRate { get; set; }

    /// <summary>Calculated: allocated_amount × commission_rate / 100.</summary>
    public decimal Incentives { get; set; }

    /// <summary>Parent document type (e.g., "SalesInvoice", "SalesOrder").</summary>
    public string ParentType { get; set; } = null!;

    /// <summary>Parent document ID.</summary>
    public Guid ParentId { get; set; }

    protected SalesTeamEntry() { }

    public SalesTeamEntry(Guid id, Guid salesPersonId, string parentType, Guid parentId,
        decimal allocatedPercentage, decimal eligibleAmount, decimal commissionRate) : base(id)
    {
        SalesPersonId = salesPersonId;
        ParentType = parentType;
        ParentId = parentId;
        AllocatedPercentage = allocatedPercentage;
        CommissionRate = commissionRate;
        AllocatedAmount = Math.Round(eligibleAmount * allocatedPercentage / 100m, 2);
        Incentives = Math.Round(AllocatedAmount * commissionRate / 100m, 2);
    }
}
