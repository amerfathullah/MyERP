using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

public class OpportunityTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateOpportunityTypeDto
{
    [Required]
    [StringLength(OpportunityTypeConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(OpportunityTypeConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetOpportunityTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
