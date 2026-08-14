using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockValuationSummaryDto
{
    public Guid CompanyId { get; set; }
    public decimal TotalStockValue { get; set; }
    public int TotalItems { get; set; }
    public int TotalWarehouses { get; set; }
    public List<StockValuationRowDto> Rows { get; set; } = new();
}

public class StockValuationRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
}
