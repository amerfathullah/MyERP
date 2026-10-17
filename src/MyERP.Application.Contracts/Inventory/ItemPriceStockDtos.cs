using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemPriceStockFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? SellingPriceListId { get; set; }
    public Guid? BuyingPriceListId { get; set; }
    public string? SearchTerm { get; set; }
    public bool InStockOnly { get; set; }
}

public class ItemPriceStockRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? ItemGroup { get; set; }
    public string? StockUom { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal StockAvailable { get; set; }
    public string? BuyingPriceListName { get; set; }
    public decimal BuyingRate { get; set; }
    public string? SellingPriceListName { get; set; }
    public decimal SellingRate { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
}

public class ItemPriceStockReportDto
{
    public List<ItemPriceStockRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalStockAvailable { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal TotalSellingValue { get; set; }
}
