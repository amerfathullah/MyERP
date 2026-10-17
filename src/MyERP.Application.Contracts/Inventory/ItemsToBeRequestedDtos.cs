using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemsToBeRequestedFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? SearchTerm { get; set; }
}

public class ItemsToBeRequestedItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemGroup { get; set; } = string.Empty;
    public Guid? ItemGroupId { get; set; }
    public string? Uom { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; }
    public decimal ActualQty { get; set; }
    public decimal IndentedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ShortfallQty { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal StandardBuyingPrice { get; set; }
    public decimal EstimatedShortfallCost { get; set; }
    public string DefaultMaterialRequestType { get; set; } = string.Empty;
}

public class ItemsToBeRequestedReportDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal TotalShortfallQty { get; set; }
    public int TotalDeficitItems { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<ItemsToBeRequestedItemDto> Items { get; set; } = new();
}
