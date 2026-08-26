using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class PartyTypeDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public PartyAccountType AccountType { get; set; }
}

public class CreateUpdatePartyTypeDto
{
    [Required]
    [StringLength(PartyTypeConsts.MaxPartyTypeNameLength)]
    public string Name { get; set; } = null!;

    [Required]
    public PartyAccountType AccountType { get; set; }
}

public class GetPartyTypeListDto : PagedAndSortedResultRequestDto
{
    public PartyAccountType? AccountType { get; set; }
    public string? Filter { get; set; }
}
