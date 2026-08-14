using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemManufacturerDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public Guid ManufacturerId { get; set; }
    public string? ManufacturerShortName { get; set; }
    public string ManufacturerPartNo { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateUpdateItemManufacturerDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid ItemId { get; set; }

    [Required]
    public Guid ManufacturerId { get; set; }

    [Required]
    [StringLength(ItemManufacturerConsts.MaxManufacturerPartNoLength)]
    public string ManufacturerPartNo { get; set; } = null!;

    [StringLength(ItemManufacturerConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
}
