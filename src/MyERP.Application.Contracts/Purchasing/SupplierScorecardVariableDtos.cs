using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class SupplierScorecardVariableDto : FullAuditedEntityDto<Guid>
{
    public string VariableLabel { get; set; } = null!;
    public string ParamName { get; set; } = null!;
    public string Path { get; set; } = null!;
    public bool IsCustom { get; set; }
    public string? Description { get; set; }
}

public class CreateUpdateSupplierScorecardVariableDto
{
    [Required]
    [StringLength(SupplierScorecardVariableConsts.MaxVariableLabelLength)]
    public string VariableLabel { get; set; } = null!;

    [Required]
    [StringLength(SupplierScorecardVariableConsts.MaxParamNameLength)]
    public string ParamName { get; set; } = null!;

    [Required]
    [StringLength(SupplierScorecardVariableConsts.MaxPathLength)]
    public string Path { get; set; } = null!;

    public bool IsCustom { get; set; }

    [StringLength(SupplierScorecardVariableConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class GetSupplierScorecardVariableListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
