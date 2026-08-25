using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class QualityInspectionParameterGroupDto : FullAuditedEntityDto<Guid>
{
    public string GroupName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateQualityInspectionParameterGroupDto
{
    [Required]
    [StringLength(QualityInspectionParameterGroupConsts.MaxGroupNameLength)]
    public string GroupName { get; set; } = null!;

    [StringLength(QualityInspectionParameterGroupConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetQualityInspectionParameterGroupListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
