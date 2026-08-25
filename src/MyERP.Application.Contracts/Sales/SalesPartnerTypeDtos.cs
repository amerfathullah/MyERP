using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class SalesPartnerTypeDto : FullAuditedEntityDto<Guid>
{
    public string PartnerTypeName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateSalesPartnerTypeDto
{
    [Required]
    [StringLength(SalesPartnerTypeConsts.MaxNameLength)]
    public string PartnerTypeName { get; set; } = null!;

    [StringLength(SalesPartnerTypeConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetSalesPartnerTypeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}
