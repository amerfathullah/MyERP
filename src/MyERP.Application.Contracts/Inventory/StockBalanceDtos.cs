using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class StockBalanceDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? ItemName { get; set; }
    public string? WarehouseName { get; set; }
    public decimal ActualQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal ReservedQtyForSubContract { get; set; }
    public decimal ReservedQtyForProductionPlan { get; set; }
    public decimal IndentedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class GetStockBalanceRequestDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    /// <summary>When true, hides items with zero stock. Default false per ERPNext PR #57458.</summary>
    public bool ExcludeZeroStock { get; set; }
}

/// <summary>Input for batch item availability check.</summary>
public class GetItemsAvailabilityInput
{
    public List<Guid> ItemIds { get; set; } = new();
    public Guid? WarehouseId { get; set; }
}

/// <summary>Per-item stock availability summary (aggregated across warehouses).</summary>
public class ItemAvailabilityDto
{
    public Guid ItemId { get; set; }
    public decimal ActualQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    /// <summary>Available for new orders = Actual - Reserved</summary>
    public decimal AvailableQty { get; set; }
}

// --- Batch-Wise Balance Report DTOs ---

public class GetBatchWiseBalanceRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IncludeZeroBalance { get; set; }
}

public class BatchWiseBalanceReportDto
{
    public List<BatchWiseBalanceRowDto> Rows { get; set; } = new();
    public int TotalBatches { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalStockValue { get; set; }
    public int ExpiredBatchCount { get; set; }
}

public class BatchWiseBalanceRowDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = "";
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal StockValue { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsDisabled { get; set; }
}
