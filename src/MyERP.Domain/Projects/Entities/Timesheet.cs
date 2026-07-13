using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Timesheet — tracks time spent by an employee on activities/projects.
/// Used for project billing (auto-populated on Sales Invoice when project is set).
/// Supports overlap validation (configurable per Projects Settings).
/// </summary>
public class Timesheet : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public TimesheetStatus Status { get; private set; } = TimesheetStatus.Draft;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Total hours across all detail rows.</summary>
    public decimal TotalHours { get; set; }

    /// <summary>Total billable hours.</summary>
    public decimal TotalBillableHours { get; set; }

    /// <summary>Total billing amount.</summary>
    public decimal TotalBillingAmount { get; set; }

    /// <summary>Total costing amount (for internal cost tracking).</summary>
    public decimal TotalCostingAmount { get; set; }

    public string? Note { get; set; }

    private readonly List<TimesheetDetail> _details = new();
    public IReadOnlyList<TimesheetDetail> Details => _details.AsReadOnly();

    protected Timesheet() { }

    public Timesheet(Guid id, Guid companyId, Guid employeeId, DateTime startDate, DateTime endDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        StartDate = startDate;
        EndDate = endDate;
        TenantId = tenantId;
    }

    public void AddDetail(TimesheetDetail detail)
    {
        if (Status != TimesheetStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _details.Add(detail);
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != TimesheetStatus.Draft || !_details.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = TimesheetStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == TimesheetStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = TimesheetStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        TotalHours = _details.Sum(d => d.Hours);
        TotalBillableHours = _details.Where(d => d.IsBillable).Sum(d => d.Hours);
        TotalBillingAmount = _details.Where(d => d.IsBillable).Sum(d => d.BillingAmount);
        TotalCostingAmount = _details.Sum(d => d.CostingAmount);
    }
}

/// <summary>Individual time log row in a timesheet.</summary>
public class TimesheetDetail : Entity<Guid>
{
    public Guid TimesheetId { get; set; }

    /// <summary>Activity type (e.g., "Development", "Consulting", "Design").</summary>
    public string ActivityType { get; set; } = null!;

    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal Hours { get; set; }

    /// <summary>Project this time was spent on.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Specific task within the project.</summary>
    public Guid? TaskId { get; set; }

    public bool IsBillable { get; set; }

    /// <summary>Billing rate per hour.</summary>
    public decimal BillingRate { get; set; }

    /// <summary>BillingRate × Hours (for billable entries).</summary>
    public decimal BillingAmount => IsBillable ? BillingRate * Hours : 0;

    /// <summary>Costing rate per hour (internal cost).</summary>
    public decimal CostingRate { get; set; }

    /// <summary>CostingRate × Hours.</summary>
    public decimal CostingAmount => CostingRate * Hours;

    /// <summary>Linked Sales Invoice (set when timesheet is billed).</summary>
    public Guid? SalesInvoiceId { get; set; }

    public string? Description { get; set; }

    protected TimesheetDetail() { }

    public TimesheetDetail(Guid id, Guid timesheetId, string activityType, DateTime fromTime, DateTime toTime, decimal hours)
        : base(id)
    {
        TimesheetId = timesheetId;
        ActivityType = activityType;
        FromTime = fromTime;
        ToTime = toTime;
        Hours = hours;
    }
}

public enum TimesheetStatus
{
    Draft = 0,
    Submitted = 1,
    Billed = 2,
    Cancelled = 3,
}
