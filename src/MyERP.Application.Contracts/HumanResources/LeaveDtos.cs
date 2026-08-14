using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class LeaveTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public decimal MaxDaysAllowed { get; set; }
    public bool IsPaidLeave { get; set; }
    public bool AllowCarryForward { get; set; }
    public bool RequiresApproval { get; set; }
}

public class CreateLeaveTypeDto
{
    [Required][StringLength(100)] public string Name { get; set; } = null!;
    [Required] public decimal MaxDaysAllowed { get; set; }
    public bool IsPaidLeave { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;
    public bool AllowCarryForward { get; set; }
    public decimal MaxCarryForwardDays { get; set; }
}

public class LeaveApplicationDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalLeaveDays { get; set; }
    public bool HalfDay { get; set; }
    public string? Reason { get; set; }
    public LeaveApplicationStatus Status { get; set; }
}

public class CreateLeaveApplicationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeaveDays { get; set; }
    public bool HalfDay { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    public Guid? LeaveApproverId { get; set; }
}

public class GetLeaveListDto : PagedAndSortedResultRequestDto
{
    public Guid? EmployeeId { get; set; }
    public LeaveApplicationStatus? Status { get; set; }
}

public class LeaveTypeDetailDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public int MaxDaysAllowed { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AllowCarryForward { get; set; }
    public int MaxCarryForwardDays { get; set; }
    public int CarryForwardExpiryMonths { get; set; }
    public bool IsPaidLeave { get; set; }
    public bool IncludeHolidays { get; set; }
    public bool AllowNegativeBalance { get; set; }
}

public class CreateUpdateLeaveTypeDto
{
    public string Name { get; set; } = null!;
    public int MaxDaysAllowed { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool AllowCarryForward { get; set; }
    public int MaxCarryForwardDays { get; set; }
    public int CarryForwardExpiryMonths { get; set; }
    public bool IsPaidLeave { get; set; } = true;
    public bool IncludeHolidays { get; set; }
    public bool AllowNegativeBalance { get; set; }
}
