using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Activity Type — categories of work for timesheet entries (e.g., Development, Consulting, Design).
/// Defines default billing and costing rates for timesheet entries.
/// 
/// Per ERPNext: Activity Type is a simple master with default rates.
/// Rate resolution: Employee-specific ActivityCost → ActivityType global rates (2-level fallback).
/// </summary>
public class ActivityType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Activity type name (e.g., "Development", "Consulting").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Default billing rate per hour (charged to customer).</summary>
    public decimal DefaultBillingRate { get; set; }

    /// <summary>Default costing rate per hour (internal cost to company).</summary>
    public decimal DefaultCostingRate { get; set; }

    /// <summary>Whether this activity type is active.</summary>
    public bool IsEnabled { get; set; } = true;

    protected ActivityType() { }

    public ActivityType(Guid id, string name, decimal defaultBillingRate = 0,
        decimal defaultCostingRate = 0, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        DefaultBillingRate = defaultBillingRate;
        DefaultCostingRate = defaultCostingRate;
        TenantId = tenantId;
    }
}

/// <summary>
/// Activity Cost — employee-specific override for billing/costing rates per activity type.
/// Takes precedence over ActivityType's default rates in the 2-level fallback.
/// 
/// Per ERPNext:
/// Level 1: ActivityCost (employee + activity_type specific)
/// Level 2: ActivityType (global default rates)
/// Currency conversion applied if employee's currency differs from company currency.
/// </summary>
public class ActivityCost : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid EmployeeId { get; set; }
    public Guid ActivityTypeId { get; set; }

    /// <summary>Billing rate per hour for this employee + activity combination.</summary>
    public decimal BillingRate { get; set; }

    /// <summary>Costing rate per hour for this employee + activity combination.</summary>
    public decimal CostingRate { get; set; }

    protected ActivityCost() { }

    public ActivityCost(Guid id, Guid employeeId, Guid activityTypeId,
        decimal billingRate, decimal costingRate, Guid? tenantId = null) : base(id)
    {
        EmployeeId = employeeId;
        ActivityTypeId = activityTypeId;
        BillingRate = billingRate;
        CostingRate = costingRate;
        TenantId = tenantId;
    }
}
