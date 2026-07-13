using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Leave Type — defines categories of leave (Annual, Sick, Maternity, etc.).
/// Configures allocation rules, carry-forward, and approval requirements.
/// </summary>
public class LeaveType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Maximum days that can be allocated per year.</summary>
    public decimal MaxDaysAllowed { get; set; }

    /// <summary>If true, employee can apply for this leave.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>If true, requires approval before granting.</summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>If true, unused leave carries forward to next year.</summary>
    public bool AllowCarryForward { get; set; }

    /// <summary>Maximum days that can be carried forward.</summary>
    public decimal MaxCarryForwardDays { get; set; }

    /// <summary>Number of months after which carry-forward leaves expire.</summary>
    public int CarryForwardExpiryMonths { get; set; }

    /// <summary>If true, counts as paid leave.</summary>
    public bool IsPaidLeave { get; set; } = true;

    /// <summary>If true, includes holidays in leave count.</summary>
    public bool IncludeHolidays { get; set; }

    /// <summary>If true, allows negative balance (advance leave).</summary>
    public bool AllowNegativeBalance { get; set; }

    protected LeaveType() { }

    public LeaveType(Guid id, string name, decimal maxDaysAllowed, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
        MaxDaysAllowed = maxDaysAllowed;
        TenantId = tenantId;
    }
}
