using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class BankDto : FullAuditedEntityDto<Guid>
{
    public string BankName { get; set; } = null!;
    public string? SwiftNumber { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateBankDto
{
    [Required]
    [StringLength(BankConsts.MaxBankNameLength)]
    public string BankName { get; set; } = null!;

    [StringLength(BankConsts.MaxSwiftNumberLength)]
    public string? SwiftNumber { get; set; }

    [StringLength(BankConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetBankListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
