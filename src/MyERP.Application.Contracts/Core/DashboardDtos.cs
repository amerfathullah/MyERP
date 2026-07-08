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
