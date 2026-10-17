using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class DelayedOrderFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public bool Consolidated { get; set; } = true;
    public bool DelayedOnly { get; set; } = true;
    public string? SearchText { get; set; }
}

public class DelayedOrderItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class DelayedOrderRowDto
{
    public Guid DeliveryNoteId { get; set; }
    public string DeliveryNumber { get; set; } = string.Empty;
    public Guid SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPoNumber { get; set; }
    public string? ShippingAddress { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime ActualDeliveryDate { get; set; }
    public int DelayedDays { get; set; }
    public decimal GrandTotal { get; set; }
    public List<DelayedOrderItemDto> Items { get; set; } = new();
}

public class DelayedOrderReportDto
{
    public List<DelayedOrderRowDto> Rows { get; set; } = new();
    public int TotalDeliveries { get; set; }
    public int DelayedDeliveries { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public double AverageDelayDays { get; set; }
    public decimal TotalDelayedAmount { get; set; }
}
