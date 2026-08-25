using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class TermsAndConditionsDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public string? Terms { get; set; }
    public bool IsSelling { get; set; }
    public bool IsBuying { get; set; }
    public bool IsDisabled { get; set; }
    public bool CopyAttachmentsToTransaction { get; set; }
}

public class CreateUpdateTermsAndConditionsDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(TermsAndConditionsConsts.MaxTitleLength)]
    public string Title { get; set; } = null!;

    [StringLength(TermsAndConditionsConsts.MaxTermsLength)]
    public string? Terms { get; set; }

    public bool IsSelling { get; set; } = true;
    public bool IsBuying { get; set; } = true;
    public bool IsDisabled { get; set; }
    public bool CopyAttachmentsToTransaction { get; set; }
}

public class GetTermsAndConditionsListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public bool? IsSelling { get; set; }
    public bool? IsBuying { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}
