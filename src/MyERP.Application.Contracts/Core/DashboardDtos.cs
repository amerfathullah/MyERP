using System;

namespace MyERP.Core;

public class DashboardSummaryDto
{
    public int TotalCustomers { get; set; }
    public int TotalSuppliers { get; set; }
    public int TotalItems { get; set; }
    public int DraftInvoices { get; set; }
    public int OutstandingInvoices { get; set; }
    public int PendingPurchaseOrders { get; set; }
    public int SubmittedEInvoices { get; set; }
    public int PendingApprovals { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
}

public class LowStockItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal ReorderLevel { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal ProjectedQty { get; set; }
}

public class RevenueTrendDto
{
    public string Month { get; set; } = null!;
    public decimal Amount { get; set; }
}

public class ExpiringBatchDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public DateTime ExpiryDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public decimal StockQty { get; set; }
    public string? WarehouseName { get; set; }
}

public class QiStatusSummaryDto
{
    public Guid PurchaseReceiptItemId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public bool InspectionRequired { get; set; }
    public string? InspectionStatus { get; set; }
    public Guid? QualityInspectionId { get; set; }
}
