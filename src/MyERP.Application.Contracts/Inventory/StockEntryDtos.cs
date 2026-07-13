using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class StockEntryDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? EntryNumber { get; set; }
    public StockEntryType EntryType { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = null!;
    public List<StockEntryItemDto> Items { get; set; } = new();
}

public class StockEntryItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public decimal? ValuationRate { get; set; }
}

public class CreateStockEntryDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public StockEntryType EntryType { get; set; }

    [Required]
    public DateTime PostingDate { get; set; }

    [StringLength(StockEntryConsts.MaxReferenceNumberLength)]
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    [StringLength(StockEntryConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateStockEntryItemDto> Items { get; set; } = new();
}

public class CreateStockEntryItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ValuationRate { get; set; }
}

/// <summary>Pre-populated items for a Manufacture stock entry from Work Order BOM.</summary>
public class ManufactureItemsDto
{
    public Guid WorkOrderId { get; set; }
    public Guid BomId { get; set; }
    public decimal ProduceQty { get; set; }
    public Guid FgItemId { get; set; }
    public Guid? FgWarehouseId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public System.Collections.Generic.List<ManufactureItemLineDto> Items { get; set; } = new();
}

public class ManufactureItemLineDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQty { get; set; }
    public decimal Rate { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public bool IsRawMaterial { get; set; }
}
