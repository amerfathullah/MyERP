using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class LetterHeadDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string LetterHeadName { get; set; } = null!;
    public LetterHeadFor LetterHeadFor { get; set; }
    public bool IsDefault { get; set; }
    public string? HeaderContent { get; set; }
    public string? FooterContent { get; set; }
    public bool IsDisabled { get; set; }
}

public class CreateUpdateLetterHeadDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(LetterHeadConsts.MaxNameLength)] public string LetterHeadName { get; set; } = null!;
    public LetterHeadFor LetterHeadFor { get; set; } = LetterHeadFor.DocType;
    public bool IsDefault { get; set; }
    public string? HeaderContent { get; set; }
    public string? FooterContent { get; set; }
    public bool IsDisabled { get; set; }
}

public class GetLetterHeadListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public LetterHeadFor? LetterHeadFor { get; set; }
}
