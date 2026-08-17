using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Support;

public class IssuePriorityDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateUpdateIssuePriorityDto
{
    [Required][StringLength(IssuePriorityConsts.MaxNameLength)] public string Name { get; set; } = null!;
    [StringLength(IssuePriorityConsts.MaxDescriptionLength)] public string? Description { get; set; }
}

public class GetIssuePriorityListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
