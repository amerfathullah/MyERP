using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class BatchWiseBalanceHistoryFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public WarehouseType? WarehouseType { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BatchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IncludeZeroStockBatches { get; set; }
    public string? SearchTerm { get; set; }
}

public class BatchWiseBalanceHistoryRowDto
{
    public Guid RowId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string StockUom { get; set; } = string.Empty;
    public decimal OpeningQty { get; set; }
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public decimal BalanceQty { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal BalanceValue { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty => Math.Max(0m, BalanceQty - ReservedQty);
}

public class BatchWiseBalanceHistoryReportDto
{
    public int TotalItemsCount { get; set; }
    public int TotalBatchesCount { get; set; }
    public decimal TotalBalanceQty { get; set; }
    public decimal TotalBalanceValue { get; set; }
    public decimal TotalReservedQty { get; set; }
    public List<BatchWiseBalanceHistoryRowDto> Rows { get; set; } = new();
}
