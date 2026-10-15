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
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
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
    public decimal TotalReservedStock { get; set; }
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
    /// <summary>
    /// Valuation rate per unit.
    /// Per ERPNext PR #59321 (Serial and batch wise stock balance report).
    /// </summary>
    public decimal ValuationRate { get; set; }
    /// <summary>
    /// Comma-separated list of active serial numbers currently in stock.
    /// Per ERPNext PR #59321 (Serial and batch wise stock balance report).
    /// </summary>
    public string? SerialNos { get; set; }
    /// <summary>
    /// Current active reserved quantity from submitted Stock Reservation Entries.
    /// Per ERPNext PR #59008 / commit 000dcfc23d.
    /// </summary>
    public decimal ReservedStockQty { get; set; }
    public DateTime? ExpiryDate { get; set; }
    /// <summary>
    /// Days until expiry. Signed integer: negative indicates days already elapsed since expiry.
    /// Per ERPNext PR #60573 / commit ae601b96c5f0 & e2d90b1448.
    /// </summary>
    public int? ExpiresInDays { get; set; }
    public bool IsExpired { get; set; }
    public bool IsDisabled { get; set; }
}

// --- Stock Balance Report DTOs (PR #60698) ---

public class GetStockBalanceReportRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool ShowDimensionWiseStock { get; set; }
    public bool ExcludeZeroStock { get; set; }
}

public class StockBalanceReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<StockBalanceReportRowDto> Rows { get; set; } = new();
    public decimal TotalOpeningQty { get; set; }
    public decimal TotalInQty { get; set; }
    public decimal TotalOutQty { get; set; }
    public decimal TotalBalanceQty { get; set; }
    public decimal TotalBalanceValue { get; set; }
}

public class StockBalanceReportRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = "";
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public decimal OpeningQty { get; set; }
    public decimal OpeningValue { get; set; }
    public decimal InQty { get; set; }
    public decimal InValue { get; set; }
    public decimal OutQty { get; set; }
    public decimal OutValue { get; set; }
    public decimal BalanceQty { get; set; }
    public decimal BalanceValue { get; set; }
    public decimal ValuationRate { get; set; }
}

// --- Product Bundle Balance Report DTOs (PR #60627) ---

public class GetProductBundleBalanceRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ParentItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class ProductBundleBalanceReportDto
{
    public List<ProductBundleBalanceRowDto> Rows { get; set; } = new();
}

public class ProductBundleBalanceRowDto
{
    public int Indent { get; set; }
    public Guid? ParentItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Uom { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public decimal ActualQty { get; set; }
    public decimal MinimumQty { get; set; }
    /// <summary>
    /// Calculated available bundle quantity. Never negative per ERPNext PR #60627 (max(0, actual_qty // minimum_qty)).
    /// </summary>
    public decimal BundleQty { get; set; }
}

