using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class BatchItemExpiryStatusRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? SearchText { get; set; }
}

public class BatchItemExpiryStatusRowDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string StockUom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ExpiryInDays { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
}

public class BatchItemExpiryStatusReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<BatchItemExpiryStatusRowDto> Rows { get; set; } = new();
    public int TotalBatches { get; set; }
    public int ExpiredCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public decimal TotalQuantity { get; set; }
}
