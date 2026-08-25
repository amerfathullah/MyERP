using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class ItemLeadTimeDto : FullAuditedEntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? StockUom { get; set; }

    public int ShiftTimeInHours { get; set; }
    public int NoOfWorkstations { get; set; }
    public int NoOfShifts { get; set; }
    public int TotalWorkstationTime { get; set; }

    public int ManufacturingTimeInMins { get; set; }
    public decimal DailyYield { get; set; }
    public int NoOfUnitsProduced { get; set; }
    public int CapacityPerDay { get; set; }

    public int PurchaseTimeDays { get; set; }
    public int BufferTimeDays { get; set; }

    public List<ItemLeadTimeSupplierDto> Suppliers { get; set; } = new();
}

public class ItemLeadTimeSupplierDto : FullAuditedEntityDto<Guid>
{
    public Guid ItemLeadTimeId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int PurchaseTimeDays { get; set; }
    public int BufferTimeDays { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateUpdateItemLeadTimeDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Range(1, 24)]
    public int ShiftTimeInHours { get; set; } = ItemLeadTimeConsts.DefaultShiftTimeInHours;

    [Range(1, 1000)]
    public int NoOfWorkstations { get; set; } = ItemLeadTimeConsts.DefaultNoOfWorkstations;

    [Range(1, 10)]
    public int NoOfShifts { get; set; } = ItemLeadTimeConsts.DefaultNoOfShifts;

    [Range(0, 1000000)]
    public int ManufacturingTimeInMins { get; set; }

    [Range(0, 100)]
    public decimal DailyYield { get; set; } = ItemLeadTimeConsts.DefaultDailyYield;

    [Range(0, 10000)]
    public int PurchaseTimeDays { get; set; }

    [Range(0, 10000)]
    public int BufferTimeDays { get; set; }

    public List<CreateUpdateItemLeadTimeSupplierDto> Suppliers { get; set; } = new();
}

public class CreateUpdateItemLeadTimeSupplierDto
{
    [Required]
    public Guid SupplierId { get; set; }

    [Range(0, 10000)]
    public int PurchaseTimeDays { get; set; }

    [Range(0, 10000)]
    public int BufferTimeDays { get; set; }

    public bool IsDefault { get; set; }
}

public class GetItemLeadTimeListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? ItemId { get; set; }
}
