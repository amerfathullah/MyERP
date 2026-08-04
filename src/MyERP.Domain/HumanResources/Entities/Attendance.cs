using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.HumanResources.Entities;

public enum AttendanceStatus
{
    Present,
    Absent,
    HalfDay,
    OnLeave
}

/// <summary>
/// Employee Attendance record for a specific date.
/// Maps to ERPNext Attendance.
/// </summary>
public class Attendance : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }

    public Guid? ShiftTypeId { get; set; }
    public DateTime? InTime { get; set; }
    public DateTime? OutTime { get; set; }
    public Guid? LeaveApplicationId { get; set; }

    protected Attendance() { }

    public Attendance(Guid id, Guid companyId, Guid employeeId, DateTime date, AttendanceStatus status, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        Date = date.Date;
        Status = status;
        TenantId = tenantId;
    }
}
