using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ShipmentParcelTemplateDto : FullAuditedEntityDto<Guid>
{
    public string ParcelTemplateName { get; set; } = null!;
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Weight { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdateShipmentParcelTemplateDto
{
    [Required]
    [StringLength(ShipmentParcelTemplateConsts.MaxParcelTemplateNameLength)]
    public string ParcelTemplateName { get; set; } = null!;

    [Range(0, 999999)]
    public decimal Length { get; set; }

    [Range(0, 999999)]
    public decimal Width { get; set; }

    [Range(0, 999999)]
    public decimal Height { get; set; }

    [Range(0, 999999)]
    public decimal Weight { get; set; }

    [StringLength(ShipmentParcelTemplateConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetShipmentParcelTemplateListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
