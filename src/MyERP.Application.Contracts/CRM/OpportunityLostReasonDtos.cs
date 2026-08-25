using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

public class OpportunityLostReasonDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDisabled { get; set; }
}

public class CreateUpdateOpportunityLostReasonDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(OpportunityLostReasonConsts.MaxReasonLength)]
    public string Reason { get; set; } = null!;

    [StringLength(OpportunityLostReasonConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsDisabled { get; set; }
}

public class GetOpportunityLostReasonListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}
