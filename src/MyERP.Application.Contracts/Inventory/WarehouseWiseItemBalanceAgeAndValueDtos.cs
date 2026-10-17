using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class WarehouseWiseItemBalanceAgeAndValueFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool FilterTotalZeroQty { get; set; } = true;
    public string? SearchTerm { get; set; }
}

public class WarehouseColumnDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
}

public class WarehouseWiseItemBalanceAgeAndValueRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public string? Brand { get; set; }
    public string? StockUom { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal AverageAgeDays { get; set; }
    public decimal TotalQty { get; set; }
    public Dictionary<Guid, decimal> WarehouseBalances { get; set; } = new();
}

public class WarehouseWiseItemBalanceAgeAndValueReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime AsOfDate { get; set; }
    public List<WarehouseColumnDto> WarehouseColumns { get; set; } = new();
    public List<WarehouseWiseItemBalanceAgeAndValueRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal AverageAgeDays { get; set; }
}
