using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class AttendanceDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public Guid? ShiftTypeId { get; set; }
    public DateTime? InTime { get; set; }
    public DateTime? OutTime { get; set; }
    public Guid? LeaveApplicationId { get; set; }
}

public class CreateAttendanceDto
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public Guid? ShiftTypeId { get; set; }
    public DateTime? InTime { get; set; }
    public DateTime? OutTime { get; set; }
}

public class GetAttendanceListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public AttendanceStatus? Status { get; set; }
}
