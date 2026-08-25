using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Projects;

public class TaskTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public decimal Weight { get; set; }
    public string? Description { get; set; }
}

public class CreateUpdateTaskTypeDto
{
    [Required]
    [StringLength(TaskTypeConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [Range(0, 1000)]
    public decimal Weight { get; set; } = 1;

    [StringLength(TaskTypeConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class GetTaskTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
