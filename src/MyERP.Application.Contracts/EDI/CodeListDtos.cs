using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.EDI;

public class CodeListDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string? CanonicalUri { get; set; }
    public string? Url { get; set; }
    public string? DefaultCommonCode { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? PublisherId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateCodeListDto
{
    [Required]
    [StringLength(EDIConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    [StringLength(EDIConsts.MaxUriLength)]
    public string? CanonicalUri { get; set; }

    [StringLength(EDIConsts.MaxUrlLength)]
    public string? Url { get; set; }

    [StringLength(EDIConsts.MaxCodeLength)]
    public string? DefaultCommonCode { get; set; }

    [StringLength(EDIConsts.MaxVersionLength)]
    public string? Version { get; set; }

    [StringLength(EDIConsts.MaxPublisherLength)]
    public string? Publisher { get; set; }

    [StringLength(EDIConsts.MaxPublisherIdLength)]
    public string? PublisherId { get; set; }

    [StringLength(EDIConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetCodeListListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Publisher { get; set; }
    public bool? IsActive { get; set; }
}
