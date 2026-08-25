using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Projects;

public class ActivityCostDto : FullAuditedEntityDto<Guid>
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public Guid ActivityTypeId { get; set; }
    public string? ActivityTypeName { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
}

public class CreateUpdateActivityCostDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ActivityTypeId { get; set; }

    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
}

public class GetActivityCostListDto : PagedAndSortedResultRequestDto
{
    public Guid? EmployeeId { get; set; }
    public Guid? ActivityTypeId { get; set; }
}
