using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankAccountSubtypeDto : FullAuditedEntityDto<Guid>
{
    public string AccountSubtypeName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateBankAccountSubtypeDto
{
    [Required]
    [StringLength(BankAccountSubtypeConsts.MaxAccountSubtypeLength)]
    public string AccountSubtypeName { get; set; } = null!;

    [StringLength(BankAccountSubtypeConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetBankAccountSubtypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
