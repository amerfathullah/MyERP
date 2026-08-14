using System;
using System.Collections.Generic;
using MyERP.Shared;

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

public class RevenueVsExpenseDto
{
    public string Month { get; set; } = null!;
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit => Revenue - Expenses;
    public decimal ProfitMarginPct => Revenue > 0 ? Math.Round((Revenue - Expenses) / Revenue * 100, 1) : 0;
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

public class ProfitMarginTrendDto
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercentage { get; set; }
}

public class UpcomingPaymentDuesDto
{
    public decimal ReceivablesDueIn7Days { get; set; }
    public decimal ReceivablesDueIn14Days { get; set; }
    public decimal ReceivablesDueIn30Days { get; set; }
    public decimal ReceivablesOverdue { get; set; }
    public decimal PayablesDueIn7Days { get; set; }
    public decimal PayablesDueIn14Days { get; set; }
    public decimal PayablesDueIn30Days { get; set; }
    public decimal PayablesOverdue { get; set; }
    public int ReceivableInvoiceCount { get; set; }
    public int PayableInvoiceCount { get; set; }
}

public class AgingSummaryWidgetDto
{
    public AgingBucketsDto Receivables { get; set; } = new();
    public AgingBucketsDto Payables { get; set; } = new();
}

public class AgingBucketsDto
{
    public decimal Current { get; set; }
    public decimal ThirtyOneToSixty { get; set; }
    public decimal SixtyOneToNinety { get; set; }
    public decimal NinetyPlus { get; set; }
    public decimal Total { get; set; }
}

public class OverdueAlertsDto
{
    public int OverdueReceivableCount { get; set; }
    public decimal OverdueReceivableAmount { get; set; }
    public int OverduePayableCount { get; set; }
    public decimal OverduePayableAmount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int OverduePurchaseOrderCount { get; set; }
}

public class FinancialKpiDto
{
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public decimal ArOutstanding { get; set; }
    public decimal ApOutstanding { get; set; }
    public decimal NetCashPosition { get; set; }
    public decimal RevenueGrowth { get; set; }
    public int InvoiceCount { get; set; }
    public int BillCount { get; set; }
    public string PeriodLabel { get; set; } = null!;
}

public class TopCustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "—";
    public decimal Revenue { get; set; }
    public int InvoiceCount { get; set; }
}

public class PendingOrdersSummaryDto
{
    public int SalesOrdersToDeliverAndBill { get; set; }
    public int SalesOrdersToDeliver { get; set; }
    public int SalesOrdersToBill { get; set; }
    public int TotalActiveSalesOrders { get; set; }
    public int PurchaseOrdersToReceiveAndBill { get; set; }
    public int PurchaseOrdersToReceive { get; set; }
    public int PurchaseOrdersToBill { get; set; }
    public int TotalActivePurchaseOrders { get; set; }
}

public class ProductionSummaryDto
{
    public int Draft { get; set; }
    public int NotStarted { get; set; }
    public int InProcess { get; set; }
    public int Completed { get; set; }
    public int Stopped { get; set; }
    public int TotalActiveOrders { get; set; }
    public decimal TotalProducedThisMonth { get; set; }
}

/// <summary>
/// Operational metrics for admin monitoring.
/// Shows system health indicators and pending action items.
/// </summary>
public class OperationalMetricsDto
{
    // Document Counts
    public int DraftDocuments { get; set; }
    public int PendingApprovals { get; set; }
    public int OverdueInvoices { get; set; }
    public int LowStockItems { get; set; }

    // Financial Health
    public decimal TotalArOutstanding { get; set; }
    public decimal TotalApOutstanding { get; set; }
    public decimal OldestUnpaidInvoiceDays { get; set; }

    // Operations
    public int ActiveSubscriptions { get; set; }
    public int OpenWorkOrders { get; set; }
    public int PendingMaterialRequests { get; set; }

    // Data Quality
    public int ItemsWithoutPrice { get; set; }
    public int CustomersWithoutContact { get; set; }

    // Last Processing
    public DateTime? LastNightlyRunDate { get; set; }
}

public class StockValuationWidgetDto
{
    public decimal TotalStockValue { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public List<StockValuationItemDto> TopItemsByValue { get; set; } = new();
}

public class StockValuationItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
}

public class BankBalanceWidgetDto
{
    public decimal TotalCashAndBank { get; set; }
    public List<BankAccountBalanceDto> Accounts { get; set; } = new();
}

public class BankAccountBalanceDto
{
    public string AccountName { get; set; } = null!;
    public string AccountCode { get; set; } = null!;
    public decimal Balance { get; set; }
    public string AccountType { get; set; } = null!;
}

public class CashFlowSnapshotDto
{
    /// <summary>Expected collections from customers in next 30 days (from SI due dates).</summary>
    public decimal ExpectedInflows30Days { get; set; }
    /// <summary>Expected payments to suppliers in next 30 days (from PI due dates).</summary>
    public decimal ExpectedOutflows30Days { get; set; }
    /// <summary>Net position = Inflows - Outflows (positive = surplus, negative = shortfall).</summary>
    public decimal NetCashFlow30Days { get; set; }
    public int InflowInvoiceCount { get; set; }
    public int OutflowInvoiceCount { get; set; }
    /// <summary>Past-due receivables (overdue SI outstanding).</summary>
    public decimal OverdueReceivables { get; set; }
    /// <summary>Past-due payables (overdue PI outstanding).</summary>
    public decimal OverduePayables { get; set; }
    public int OverdueReceivableCount { get; set; }
    public int OverduePayableCount { get; set; }
}

public class ExpiringQuotationDto
{
    public Guid QuotationId { get; set; }
    public string QuotationNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public decimal GrandTotal { get; set; }
    public DateTime ValidUntil { get; set; }
    public int DaysRemaining { get; set; }
}

public class TodaysActivityDto
{
    public int InvoicesCreated { get; set; }
    public int PaymentsReceived { get; set; }
    public int OrdersPlaced { get; set; }
    public int DeliveriesMade { get; set; }
    public int ReceiptsProcessed { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
}

public class PendingMaterialRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = "—";
    public DateTime RequestDate { get; set; }
    public DocumentStatus Status { get; set; }
    public int ItemCount { get; set; }
    public DateTime? RequiredByDate { get; set; }
}

public class TopDebtorDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "—";
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
    public DateTime? OldestDueDate { get; set; }
    public int DaysOverdue { get; set; }
}

public class DeliveryDueAlertDto
{
    public int OverdueCount { get; set; }
    public int DueThisWeekCount { get; set; }
    public int DueNext7DaysCount { get; set; }
    public decimal OverdueTotalValue { get; set; }
    public List<DeliveryDueOrderDto> OverdueOrders { get; set; } = new();
    public List<DeliveryDueOrderDto> UpcomingOrders { get; set; } = new();
}

public class DeliveryDueOrderDto
{
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = "—";
    public string SupplierName { get; set; } = "—";
    public DateTime? ExpectedDeliveryDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PerReceived { get; set; }
}

public class ReorderPointItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = "—";
    public string ItemName { get; set; } = "—";
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ShortageQty { get; set; }
    public string WarehouseName { get; set; } = "—";
}

public class ReorderPointDashboardDto
{
    public int TotalItemsBelowReorder { get; set; }
    public int CriticalItems { get; set; }
    public decimal TotalShortageValue { get; set; }
    public List<ReorderPointItemDto> Items { get; set; } = new();
}
