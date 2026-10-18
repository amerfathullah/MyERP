using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class AvailableBatchFilterDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public WarehouseType? WarehouseType { get; set; }
    public Guid? BatchId { get; set; }
    public bool IncludeExpiredBatches { get; set; }
    public string? SearchTerm { get; set; }
}

public class AvailableBatchRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? StockUom { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal BalanceQty { get; set; }
    public bool IsExpired { get; set; }
    public int? DaysToExpiry { get; set; }
}

public class AvailableBatchReportDto
{
    public DateTime ToDate { get; set; }
    public List<AvailableBatchRowDto> Rows { get; set; } = new();
    public int TotalBatches { get; set; }
    public decimal TotalBalanceQty { get; set; }
    public int ExpiredBatchesCount { get; set; }
}
