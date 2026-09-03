using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class BomStockAnalysisDto
{
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal BomQuantity { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal CanManufactureQty { get; set; }
    public bool AllMaterialsSufficient { get; set; }
    public List<BomMaterialAvailabilityDto> Materials { get; set; } = new();
}

public class BomMaterialAvailabilityDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal RequiredQtyPerUnit { get; set; }
    public decimal RequiredQtyForBatch { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal Shortage { get; set; }
    public bool IsSufficient { get; set; }
}
