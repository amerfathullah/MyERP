using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class GetItemWisePurchaseHistoryDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? ItemGroupId { get; set; }
}

public class ItemWisePurchaseHistoryRowDto
{
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierGroup { get; set; }
    public string? Project { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "Unit";
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal BilledAmount { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Currency { get; set; } = "MYR";
}

public class ItemWisePurchaseChartPointDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ItemWisePurchaseHistoryReportDto
{
    public List<ItemWisePurchaseHistoryRowDto> Rows { get; set; } = new();
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalReceivedQuantity { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public int TotalOrders { get; set; }
    public List<ItemWisePurchaseChartPointDto> ChartData { get; set; } = new();
}
