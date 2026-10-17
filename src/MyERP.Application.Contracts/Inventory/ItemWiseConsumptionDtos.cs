using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemWiseConsumptionRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SearchText { get; set; }
}

public class ItemWiseConsumptionRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string StockUom { get; set; } = "Unit";
    public decimal ConsumedQty { get; set; }
    public decimal ConsumedAmount { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal DeliveredAmount { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Suppliers { get; set; }
}

public class ItemWiseConsumptionReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<ItemWiseConsumptionRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal GrandTotalConsumedQty { get; set; }
    public decimal GrandTotalConsumedAmount { get; set; }
    public decimal GrandTotalDeliveredQty { get; set; }
    public decimal GrandTotalDeliveredAmount { get; set; }
    public decimal GrandTotalQty { get; set; }
    public decimal GrandTotalAmount { get; set; }
}
