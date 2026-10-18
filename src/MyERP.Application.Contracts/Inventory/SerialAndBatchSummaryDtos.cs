using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class SerialAndBatchSummaryFilterDto
{
    public Guid? CompanyId { get; set; }
    public string? VoucherType { get; set; }
    public string? VoucherNo { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SerialNo { get; set; }
    public Guid? BatchId { get; set; }
    public string? SearchTerm { get; set; }
    public bool IncludeCancelled { get; set; }
}

public class SerialAndBatchSummaryRowDto
{
    public Guid BundleId { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string TypeOfTransaction { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public decimal Quantity { get; set; }
    public decimal IncomingRate { get; set; }
    public decimal StockValueDifference { get; set; }
    public bool IsCancelled { get; set; }
}

public class SerialAndBatchSummaryReportDto
{
    public int TotalBundles { get; set; }
    public int TotalEntries { get; set; }
    public decimal TotalInwardQty { get; set; }
    public decimal TotalOutwardQty { get; set; }
    public decimal TotalStockValue { get; set; }
    public List<SerialAndBatchSummaryRowDto> Rows { get; set; } = new();
}
