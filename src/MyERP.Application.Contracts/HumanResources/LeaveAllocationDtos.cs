using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class LeaveAllocationDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalLeavesAllocated { get; set; }
    public decimal CarryForwardDays { get; set; }
    public decimal LeavesUsed { get; set; }
    public decimal Balance { get; set; }
}

public class CreateLeaveAllocationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeavesAllocated { get; set; }
    public decimal CarryForwardDays { get; set; }
}

public class BulkLeaveAllocationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeavesPerEmployee { get; set; }
}

public class GetLeaveAllocationListDto : PagedAndSortedResultRequestDto
{
    public Guid? EmployeeId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LeaveTypeId { get; set; }
}
