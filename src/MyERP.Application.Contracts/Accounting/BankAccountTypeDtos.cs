using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankAccountTypeDto : FullAuditedEntityDto<Guid>
{
    public string AccountTypeName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateBankAccountTypeDto
{
    [Required]
    [StringLength(BankAccountTypeConsts.MaxAccountTypeLength)]
    public string AccountTypeName { get; set; } = null!;

    [StringLength(BankAccountTypeConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetBankAccountTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
