using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class SalesPartnerDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public int PartnerType { get; set; }
    public decimal CommissionRate { get; set; }
    public Guid? TerritoryId { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? ReferralCode { get; set; }
}

public class CreateSalesPartnerDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    public int PartnerType { get; set; }

    [Range(0, 100)]
    public decimal CommissionRate { get; set; }

    public Guid? TerritoryId { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? ReferralCode { get; set; }
}

public class GetSalesPartnerListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
