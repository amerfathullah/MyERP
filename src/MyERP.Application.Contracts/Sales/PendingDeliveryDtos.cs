using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class PendingDeliveryItemDto
{
    public Guid SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal OrderedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
    public string Uom { get; set; } = null!;
    public decimal Rate { get; set; }
    public decimal PendingAmount { get; set; }
    public int DaysUntilDue { get; set; }
    public bool IsOverdue { get; set; }
    public string? WarehouseId { get; set; }
}

public class PendingDeliveryReportDto
{
    public DateTime AsOfDate { get; set; }
    public int TotalOrders { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<PendingDeliveryItemDto> Items { get; set; } = [];
}

public class PendingDeliveryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ItemId { get; set; }
    public bool OverdueOnly { get; set; }
}

public class CreateDnFromPendingDto
{
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public List<PendingItemSelectionDto> Items { get; set; } = [];
}

public class PendingItemSelectionDto
{
    public Guid SalesOrderId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateDeliveryNoteResultDto
{
    public Guid DeliveryNoteId { get; set; }
    public string DeliveryNumber { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
}
