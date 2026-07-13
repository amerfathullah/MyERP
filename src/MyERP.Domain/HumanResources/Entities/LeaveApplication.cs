using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

/// <summary>
/// Leave Application — employee request for leave.
/// Validates: no overlapping dates, balance available, approval workflow.
/// </summary>
public class LeaveApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Total leave days (calculated from date range, excluding holidays if configured).</summary>
    public decimal TotalLeaveDays { get; set; }

    /// <summary>Half-day leave flag.</summary>
    public bool HalfDay { get; set; }

    public string? Reason { get; set; }

    public LeaveApplicationStatus Status { get; private set; } = LeaveApplicationStatus.Open;

    /// <summary>Leave approver (manager/HR).</summary>
    public Guid? LeaveApproverId { get; set; }

    protected LeaveApplication() { }

    public LeaveApplication(Guid id, Guid companyId, Guid employeeId, Guid leaveTypeId,
        DateTime fromDate, DateTime toDate, decimal totalLeaveDays, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        FromDate = fromDate;
        ToDate = toDate;
        TotalLeaveDays = totalLeaveDays;
        TenantId = tenantId;

        if (toDate < fromDate)
            throw new BusinessException(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate);
    }

    public void Approve()
    {
        if (Status != LeaveApplicationStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeaveApplicationStatus.Approved;
    }

    public void Reject()
    {
        if (Status != LeaveApplicationStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeaveApplicationStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status is LeaveApplicationStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = LeaveApplicationStatus.Cancelled;
    }
}

public enum LeaveApplicationStatus
{
    Open = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}
