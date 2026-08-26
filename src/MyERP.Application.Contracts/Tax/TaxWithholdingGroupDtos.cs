using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Tax;

public class TaxWithholdingGroupDto : FullAuditedEntityDto<Guid>
{
    public string GroupName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateTaxWithholdingGroupDto
{
    [Required]
    [StringLength(TaxWithholdingGroupConsts.MaxGroupNameLength)]
    public string GroupName { get; set; } = null!;

    [StringLength(TaxWithholdingGroupConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetTaxWithholdingGroupListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
