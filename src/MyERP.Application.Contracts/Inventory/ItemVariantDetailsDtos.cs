using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemVariantDetailsFilterDto
{
    public Guid? TemplateItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class ItemVariantDetailsRowDto
{
    public Guid VariantId { get; set; }
    public string VariantCode { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
    public decimal AvgBuyingPrice { get; set; }
    public decimal AvgSellingPrice { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal InProduction { get; set; }
    public int OpenOrders { get; set; }
    public string Currency { get; set; } = "MYR";
}

public class ItemVariantDetailsReportDto
{
    public Guid? TemplateItemId { get; set; }
    public string TemplateItemCode { get; set; } = string.Empty;
    public string TemplateItemName { get; set; } = string.Empty;
    public List<string> AttributeNames { get; set; } = new();
    public List<ItemVariantDetailsRowDto> Rows { get; set; } = new();
}
