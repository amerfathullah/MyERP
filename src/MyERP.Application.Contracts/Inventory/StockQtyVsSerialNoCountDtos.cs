using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockQtyVsSerialNoCountFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public bool ShowDisabledItems { get; set; }
    public bool ShowOnlyDifferences { get; set; }
    public string? SearchTerm { get; set; }
}

public class StockQtyVsSerialNoCountRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal SerialNoCount { get; set; }
    public decimal StockQty { get; set; }
    public decimal Difference { get; set; }
}

public class StockQtyVsSerialNoCountReportDto
{
    public List<StockQtyVsSerialNoCountRowDto> Rows { get; set; } = new();
    public int TotalItems => Rows.Count;
    public int TotalMismatches { get; set; }
    public decimal TotalSerialCount { get; set; }
    public decimal TotalStockQty { get; set; }
    public decimal TotalDifference { get; set; }
}

public class SyncSerialNoStatusInputDto
{
    public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
}

public class SyncSerialNoStatusResultDto
{
    public int SyncedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
