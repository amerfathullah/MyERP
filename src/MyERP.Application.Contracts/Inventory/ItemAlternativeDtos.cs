using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemAlternativeDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public Guid AlternativeItemId { get; set; }
    public string? AlternativeItemCode { get; set; }
    public string? AlternativeItemName { get; set; }
    public bool TwoWay { get; set; }
}

public class CreateUpdateItemAlternativeDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid ItemId { get; set; }

    [Required]
    public Guid AlternativeItemId { get; set; }

    public bool TwoWay { get; set; }
}
