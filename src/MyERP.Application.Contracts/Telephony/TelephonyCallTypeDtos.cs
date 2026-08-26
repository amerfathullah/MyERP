using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Telephony;

public class TelephonyCallTypeDto : FullAuditedEntityDto<Guid>
{
    public string CallTypeName { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class CreateUpdateTelephonyCallTypeDto
{
    [Required]
    [StringLength(TelephonyConsts.MaxCallTypeNameLength)]
    public string CallTypeName { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class GetTelephonyCallTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
