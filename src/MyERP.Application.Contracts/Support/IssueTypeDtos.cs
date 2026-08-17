using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Support;

public class IssueTypeDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateUpdateIssueTypeDto
{
    [Required][StringLength(IssueTypeConsts.MaxNameLength)] public string Name { get; set; } = null!;
    [StringLength(IssueTypeConsts.MaxDescriptionLength)] public string? Description { get; set; }
}

public class GetIssueTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
