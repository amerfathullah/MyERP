using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockAgeingFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Ranges { get; set; } = "30, 60, 90, 120";
    public bool ShowWarehouseWiseStock { get; set; } = true;
    public bool IncludeZeroStock { get; set; } = false;
    public string? FilterText { get; set; }
}

public class StockAgeingBucketDefinitionDto
{
    public int BucketIndex { get; set; }
    public string Label { get; set; } = null!;
    public int MinDays { get; set; }
    public int? MaxDays { get; set; }
}

public class StockAgeingBucketValueDto
{
    public int BucketIndex { get; set; }
    public string Label { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal StockValue { get; set; }
}

public class StockAgeingRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string? ItemGroup { get; set; }
    public string? Brand { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal TotalQty { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal AverageAgeDays { get; set; }
    public int OldestDays { get; set; }
    public int NewestDays { get; set; }
    public string StockUom { get; set; } = "Unit";
    public List<StockAgeingBucketValueDto> Buckets { get; set; } = new();
}

public class StockAgeingReportDto
{
    public DateTime ToDate { get; set; }
    public List<StockAgeingBucketDefinitionDto> Buckets { get; set; } = new();
    public List<StockAgeingRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal OverallAverageAgeDays { get; set; }
    public int AgedOver90Count { get; set; }
}
