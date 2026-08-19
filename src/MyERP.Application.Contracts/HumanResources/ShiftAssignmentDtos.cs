using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class ShiftAssignmentDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid ShiftTypeId { get; set; }
    public string? ShiftTypeName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ShiftAssignmentStatus Status { get; set; }
}

public class CreateShiftAssignmentDto
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ShiftTypeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class GetShiftAssignmentListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }
}
