using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class QuotationLostReasonDto : FullAuditedEntityDto<Guid>
{
    public string Reason { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateQuotationLostReasonDto
{
    [Required]
    [StringLength(QuotationLostReasonConsts.MaxReasonLength)]
    public string Reason { get; set; } = null!;

    [StringLength(QuotationLostReasonConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetQuotationLostReasonListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
