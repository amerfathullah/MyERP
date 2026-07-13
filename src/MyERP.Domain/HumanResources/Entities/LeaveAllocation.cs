using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Leave Allocation — assigns leave balance to an employee for a specific period.
/// Each allocation represents available leaves for one leave type per year.
/// Balance = TotalLeavesAllocated + CarryForwardDays - used (from LeaveLedgerEntry).
/// </summary>
public class LeaveAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }

    /// <summary>Period start (typically fiscal year or calendar year start).</summary>
    public DateTime FromDate { get; set; }

    /// <summary>Period end.</summary>
    public DateTime ToDate { get; set; }

    /// <summary>Total leaves allocated for this period.</summary>
    public decimal TotalLeavesAllocated { get; set; }

    /// <summary>Days carried forward from previous period.</summary>
    public decimal CarryForwardDays { get; set; }

    /// <summary>Leaves already used (updated on approval/cancellation).</summary>
    public decimal LeavesUsed { get; set; }

    /// <summary>Current balance = allocated + carry_forward - used.</summary>
    public decimal Balance => TotalLeavesAllocated + CarryForwardDays - LeavesUsed;

    /// <summary>New leaves = allocated - used (excluding carry forward).</summary>
    public decimal NewLeavesAllocated => TotalLeavesAllocated - LeavesUsed;

    protected LeaveAllocation() { }

    public LeaveAllocation(Guid id, Guid companyId, Guid employeeId, Guid leaveTypeId,
        DateTime fromDate, DateTime toDate, decimal totalLeavesAllocated, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        TotalLeavesAllocated = totalLeavesAllocated;
        TenantId = tenantId;
    }

    /// <summary>Deduct leave days when an application is approved.</summary>
    public void DeductLeave(decimal days)
    {
        LeavesUsed += days;
    }

    /// <summary>Restore leave days when an approved application is cancelled.</summary>
    public void RestoreLeave(decimal days)
    {
        LeavesUsed = Math.Max(0, LeavesUsed - days);
    }
}
