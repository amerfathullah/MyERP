using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class DelayedItemFilterDto
{
    [Required]
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Document type to base report on: "Delivery Note" or "Sales Invoice".
    /// Defaults to "Delivery Note".
    /// </summary>
    public string BasedOn { get; set; } = "Delivery Note";

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? ItemGroupId { get; set; }

    public Guid? SalesOrderId { get; set; }

    /// <summary>
    /// When true, only includes delayed lines (DelayedDays > 0).
    /// Defaults to true.
    /// </summary>
    public bool DelayedOnly { get; set; } = true;

    public string? SearchTerm { get; set; }
}

public class DelayedItemRowDto
{
    public string VoucherType { get; set; } = "Delivery Note";
    public Guid VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? ShippingAddress { get; set; }
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public string? CustomerPoNumber { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemGroup { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime ActualDeliveryDate { get; set; }
    public int DelayedDays { get; set; }
}

public class DelayedItemReportDto
{
    public Guid CompanyId { get; set; }
    public string BasedOn { get; set; } = "Delivery Note";
    public int TotalItems { get; set; }
    public int DelayedItems { get; set; }
    public decimal OnTimeRate { get; set; }
    public double AverageDelayDays { get; set; }
    public decimal TotalDelayedAmount { get; set; }
    public List<DelayedItemRowDto> Rows { get; set; } = new();
}
