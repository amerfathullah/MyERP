using System;
using System.Collections.Generic;

namespace MyERP.Core;

/// <summary>
/// Customer performance metrics for the Customer detail page.
/// Per ERPNext: Customer dashboard shows revenue, orders, payment trends.
/// </summary>
public class CustomerPerformanceDto
{
    // Revenue Metrics
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal RevenueLastMonth { get; set; }
    public decimal RevenueGrowthPercent { get; set; }

    // Order Metrics
    public int TotalOrders { get; set; }
    public int OrdersThisMonth { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Payment Metrics
    public decimal AverageDaysToPayment { get; set; }
    public int OnTimePaymentPercent { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal TotalOverdueAmount { get; set; }

    // Credit
    public decimal CreditLimit { get; set; }
    public decimal CreditUsed { get; set; }
    public int CreditUtilizationPercent { get; set; }

    // Trend (last 6 months)
    public List<MonthlyRevenuePoint> RevenueTrend { get; set; } = new();
}

/// <summary>
/// Supplier performance metrics for the Supplier detail page.
/// Per ERPNext: Supplier scorecard shows delivery, quality, cost metrics.
/// </summary>
public class SupplierPerformanceDto
{
    // Spend Metrics
    public decimal TotalSpend { get; set; }
    public decimal SpendThisMonth { get; set; }
    public decimal SpendLastMonth { get; set; }

    // Order Metrics
    public int TotalOrders { get; set; }
    public int OrdersThisMonth { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Delivery Metrics
    public decimal AverageLeadTimeDays { get; set; }
    public int OnTimeDeliveryPercent { get; set; }
    public int PendingReceiptCount { get; set; }

    // Payment Metrics
    public decimal TotalOutstandingPayable { get; set; }
    public int OverduePayableCount { get; set; }

    // Trend (last 6 months)
    public List<MonthlyRevenuePoint> SpendTrend { get; set; } = new();
}

public class MonthlyRevenuePoint
{
    public string Month { get; set; } = null!;
    public decimal Amount { get; set; }
}

/// <summary>
/// PO line-item fulfillment status for the Pending Fulfillment report.
/// Shows ordered → received → invoiced per PO line item.
/// </summary>
public class PoFulfillmentItemDto
{
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public string SupplierName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal PendingReceiptQty { get; set; }
    public decimal PendingBillingQty { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public string FulfillmentStatus { get; set; } = null!; // "Ordered", "PartiallyReceived", "FullyReceived", "FullyBilled"
}

public class PoFulfillmentReportDto
{
    public int TotalItems { get; set; }
    public int PendingReceiptItems { get; set; }
    public int PendingBillingItems { get; set; }
    public int OverdueItems { get; set; }
    public decimal TotalPendingValue { get; set; }
    public List<PoFulfillmentItemDto> Items { get; set; } = new();
}
