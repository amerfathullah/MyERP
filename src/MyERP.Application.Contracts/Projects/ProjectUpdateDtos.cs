using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Projects;

public class ProjectUpdateDto : FullAuditedEntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string? ProjectNumber { get; set; }
    public string? ProjectName { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }
    public decimal PercentComplete { get; set; }
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public bool Sent { get; set; }
}

public class CreateUpdateProjectUpdateDto
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    public TimeSpan? Time { get; set; }

    [Range(0, 100)]
    public decimal PercentComplete { get; set; }

    [StringLength(ProjectUpdateConsts.MaxSummaryLength)]
    public string? Summary { get; set; }

    [StringLength(ProjectUpdateConsts.MaxNotesLength)]
    public string? Notes { get; set; }

    public bool Sent { get; set; }
}

public class GetProjectUpdateListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? ProjectId { get; set; }
}
