using System;
using System.Collections.Generic;

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

public class QuickReorderDto
{
    public Guid CompanyId { get; set; }
    public List<Guid> ItemIds { get; set; } = new();
}

public class QuickReorderResultDto
{
    public Guid MaterialRequestId { get; set; }
    public string MaterialRequestNumber { get; set; } = null!;
    public int ItemCount { get; set; }
}

public class SupplierPerformanceWidgetDto
{
    public int TotalSuppliers { get; set; }
    public decimal OverallOnTimeRate { get; set; }
    public int SuppliersAtRisk { get; set; }
    public List<SupplierPerformanceItemDto> Suppliers { get; set; } = new();
}

public class SupplierPerformanceItemDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "—";
    public int TotalOrders { get; set; }
    public int OnTimeCount { get; set; }
    public int LateCount { get; set; }
    public decimal OnTimeRate { get; set; }
    public decimal TotalValue { get; set; }
}
