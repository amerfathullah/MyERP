using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class SerialAndBatchWiseStockBalanceFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IncludeZeroStockItems { get; set; }
    public string? SearchTerm { get; set; }
}

public class SerialAndBatchWiseStockBalanceRowDto
{
    public Guid RowId { get; set; }
    public int Indent { get; set; } // 0 for parent Item row, 1 for child Batch row
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string StockUom { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? SerialNos { get; set; }
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

public class SerialAndBatchWiseStockBalanceReportDto
{
    public int TotalItemsCount { get; set; }
    public int TotalBatchesCount { get; set; }
    public decimal TotalBalanceQty { get; set; }
    public decimal TotalBalanceValue { get; set; }
    public List<SerialAndBatchWiseStockBalanceRowDto> Rows { get; set; } = new();
}
