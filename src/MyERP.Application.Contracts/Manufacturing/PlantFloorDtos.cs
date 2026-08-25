using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class PlantFloorDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string FloorName { get; set; } = null!;
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUpdatePlantFloorDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(PlantFloorConsts.MaxFloorNameLength)]
    public string FloorName { get; set; } = null!;

    public Guid? WarehouseId { get; set; }

    [StringLength(PlantFloorConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class GetPlantFloorListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}
