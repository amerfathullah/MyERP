using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class EmployeeGroupDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string GroupName { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public List<EmployeeGroupItemDto> Items { get; set; } = new();
}

public class EmployeeGroupItemDto : EntityDto<Guid>
{
    public Guid EmployeeGroupId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public string? Designation { get; set; }
}

public class CreateUpdateEmployeeGroupDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(EmployeeGroupConsts.MaxGroupNameLength)]
    public string GroupName { get; set; } = null!;

    public bool IsDisabled { get; set; }
    public List<CreateUpdateEmployeeGroupItemDto> Items { get; set; } = new();
}

public class CreateUpdateEmployeeGroupItemDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public string EmployeeName { get; set; } = null!;

    public string? Designation { get; set; }
}

public class GetEmployeeGroupListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}
