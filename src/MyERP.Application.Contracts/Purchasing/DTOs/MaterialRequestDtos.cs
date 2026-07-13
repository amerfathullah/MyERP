using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyERP.Core;
using MyERP.Purchasing;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing.DTOs;

public class MaterialRequestDto
{
    public Guid Id { get; set; }
    public string? RequestNumber { get; set; }
    public MaterialRequestType RequestType { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreationTime { get; set; }
    public List<MaterialRequestItemDto> Items { get; set; } = new();
}

public class MaterialRequestItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string? Uom { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class CreateMaterialRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public MaterialRequestType RequestType { get; set; }

    [Required]
    public DateTime RequestDate { get; set; }

    public DateTime? RequiredByDate { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    [StringLength(MaterialRequestConsts.MaxNotesLength)]
    public string? Notes { get; set; }

    public List<CreateMaterialRequestItemDto> Items { get; set; } = new();
}

public class CreateMaterialRequestItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [StringLength(128)]
    public string ItemName { get; set; } = null!;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [StringLength(20)]
    public string Uom { get; set; } = "Unit";

    public Guid? WarehouseId { get; set; }
}

public class GetMaterialRequestListDto : PagedAndSortedResultRequestDto
{
    public MaterialRequestType? RequestType { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
    public string? Status { get; set; }
}
