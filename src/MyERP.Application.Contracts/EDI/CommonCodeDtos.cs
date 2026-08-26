using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.EDI;

public class CommonCodeDto : FullAuditedEntityDto<Guid>
{
    public Guid CodeListId { get; set; }
    public string Title { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public string? AdditionalDataJson { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateCommonCodeDto
{
    [Required]
    public Guid CodeListId { get; set; }

    [Required]
    [StringLength(EDIConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(EDIConsts.MaxCodeLength)]
    public string Code { get; set; } = null!;

    [StringLength(EDIConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public string? AdditionalDataJson { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetCommonCodeListDto : PagedAndSortedResultRequestDto
{
    public Guid? CodeListId { get; set; }
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
