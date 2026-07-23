using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.EInvoice.Entities;
using MyERP.Workflow;
using MyERP.Workflow.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class DashboardAppService : ApplicationService
{
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo;
    private readonly IRepository<EInvoiceSubmission, Guid> _eInvoiceRepo;
    private readonly IRepository<ApprovalRequest, Guid> _approvalRepo;

    public DashboardAppService(
        IRepository<Customer, Guid> customerRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Bin, Guid> binRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepo,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepo,
        IRepository<EInvoiceSubmission, Guid> eInvoiceRepo,
        IRepository<ApprovalRequest, Guid> approvalRepo)
    {
        _customerRepo = customerRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _binRepo = binRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _purchaseInvoiceRepo = purchaseInvoiceRepo;
        _purchaseOrderRepo = purchaseOrderRepo;
        _eInvoiceRepo = eInvoiceRepo;
        _approvalRepo = approvalRepo;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();

        var monthlyRevenue = siQuery
            .Where(i => i.Status == DocumentStatus.Posted && i.IssueDate >= monthStart)
            .Sum(i => i.GrandTotal);

        var monthlyExpenses = piQuery
            .Where(i => i.Status == DocumentStatus.Posted && i.IssueDate >= monthStart)
            .Sum(i => i.GrandTotal);

        return new DashboardSummaryDto
        {
            TotalCustomers = (int)await _customerRepo.GetCountAsync(),
            TotalSuppliers = (int)await _supplierRepo.GetCountAsync(),
            TotalItems = (int)await _itemRepo.GetCountAsync(),
            DraftInvoices = (int)await _salesInvoiceRepo.CountAsync(i => i.Status == DocumentStatus.Draft),
            OutstandingInvoices = (int)await _salesInvoiceRepo.CountAsync(i => i.Status == DocumentStatus.Posted && i.AmountPaid < i.GrandTotal),
            PendingPurchaseOrders = (int)await _purchaseOrderRepo.CountAsync(po =>
                po.Status == DocumentStatus.ToDeliverAndBill || po.Status == DocumentStatus.ToDeliver || po.Status == DocumentStatus.ToBill),
            SubmittedEInvoices = (int)await _eInvoiceRepo.GetCountAsync(),
            PendingApprovals = (int)await _approvalRepo.CountAsync(a => a.Status == ApprovalStatus.Pending),
            MonthlyRevenue = monthlyRevenue,
            MonthlyExpenses = monthlyExpenses,
        };
    }

    /// <summary>
    /// Returns items whose projected qty is at or below their reorder level.
    /// Used by the dashboard low-stock alert widget.
    /// </summary>
    public async Task<List<LowStockItemDto>> GetLowStockItemsAsync()
    {
        var items = await _itemRepo.GetListAsync(i => i.ReorderLevel > 0 && i.IsActive);
        if (!items.Any()) return new List<LowStockItemDto>();

        var itemIds = items.Select(i => i.Id).ToHashSet();
        var binQuery = await _binRepo.GetQueryableAsync();
        // Batch query: get all bins for reorder-eligible items in one DB call
        var relevantBins = binQuery.Where(b => itemIds.Contains(b.ItemId)).ToList();
        var binsByItem = relevantBins.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<LowStockItemDto>();
        foreach (var item in items)
        {
            var itemBins = binsByItem.GetValueOrDefault(item.Id, new List<Bin>());
            var totalProjected = itemBins.Sum(b => b.ProjectedQty);

            if (totalProjected <= item.ReorderLevel)
            {
                result.Add(new LowStockItemDto
                {
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    ReorderLevel = item.ReorderLevel,
                    CurrentStock = itemBins.Sum(b => b.ActualQty),
                    ProjectedQty = totalProjected,
                });
            }
        }
        return result.OrderBy(x => x.ProjectedQty).Take(20).ToList();
    }

    /// <summary>
    /// Returns last 6 months of revenue (sum of posted SI GrandTotal per month).
    /// </summary>
    public async Task<List<RevenueTrendDto>> GetRevenueTrendAsync()
    {
        var sixMonthsAgo = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);
        var query = await _salesInvoiceRepo.GetQueryableAsync();

        // Server-side aggregation — only fetches year/month/sum, not full entity rows
        var trend = query
            .Where(i => i.Status == DocumentStatus.Posted && i.IssueDate >= sixMonthsAgo)
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new RevenueTrendDto
            {
                Month = g.Key.Year + "-" + g.Key.Month.ToString().PadLeft(2, '0'),
                Amount = g.Sum(i => i.GrandTotal),
            })
            .ToList();

        // Fill in missing months with 0
        var result = new List<RevenueTrendDto>();
        for (int i = 0; i < 6; i++)
        {
            var d = sixMonthsAgo.AddMonths(i);
            var key = $"{d.Year}-{d.Month:D2}";
            var existing = trend.FirstOrDefault(t => t.Month == key);
            result.Add(existing ?? new RevenueTrendDto { Month = key, Amount = 0 });
        }
        return result;
    }

    /// <summary>
    /// Financial KPIs for the current month — the numbers every business owner needs at a glance.
    /// Shows: Revenue, Expenses, Net Profit, Cash Position, AR Outstanding, AP Outstanding.
    /// </summary>
    public async Task<FinancialKpiDto> GetFinancialKpisAsync(Guid companyId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();

        // Current month revenue — server-side sum, no entity materialization
        var monthSiQuery = siQuery.Where(si =>
            si.CompanyId == companyId && si.Status == DocumentStatus.Posted &&
            si.IssueDate >= monthStart && si.IssueDate <= monthEnd && !si.IsReturn);
        var monthlyRevenue = monthSiQuery.Select(si => si.GrandTotal).DefaultIfEmpty(0).Sum();
        var invoiceCount = monthSiQuery.Count();

        // Current month expenses
        var monthPiQuery = piQuery.Where(pi =>
            pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted &&
            pi.IssueDate >= monthStart && pi.IssueDate <= monthEnd && !pi.IsReturn);
        var monthlyExpenses = monthPiQuery.Select(pi => pi.GrandTotal).DefaultIfEmpty(0).Sum();
        var billCount = monthPiQuery.Count();

        var netProfit = monthlyRevenue - monthlyExpenses;

        // AR/AP outstanding — server-side sum
        var arOutstanding = siQuery.Where(si =>
            si.CompanyId == companyId && si.Status == DocumentStatus.Posted && !si.IsReturn)
            .Select(si => si.GrandTotal - si.AmountPaid).DefaultIfEmpty(0).Sum();

        var apOutstanding = piQuery.Where(pi =>
            pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted && !pi.IsReturn)
            .Select(pi => pi.GrandTotal - pi.AmountPaid).DefaultIfEmpty(0).Sum();

        var netCashPosition = arOutstanding - apOutstanding;

        // Previous month revenue for growth calculation
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthEnd = monthStart.AddDays(-1);
        var prevMonthRevenue = siQuery.Where(si =>
            si.CompanyId == companyId && si.Status == DocumentStatus.Posted &&
            si.IssueDate >= prevMonthStart && si.IssueDate <= prevMonthEnd && !si.IsReturn)
            .Select(si => si.GrandTotal).DefaultIfEmpty(0).Sum();

        decimal revenueGrowth = prevMonthRevenue > 0
            ? Math.Round((monthlyRevenue - prevMonthRevenue) / prevMonthRevenue * 100, 1)
            : (monthlyRevenue > 0 ? 100m : 0m);

        return new FinancialKpiDto
        {
            MonthlyRevenue = monthlyRevenue,
            MonthlyExpenses = monthlyExpenses,
            NetProfit = netProfit,
            ProfitMargin = monthlyRevenue > 0 ? Math.Round(netProfit / monthlyRevenue * 100, 1) : 0,
            ArOutstanding = arOutstanding,
            ApOutstanding = apOutstanding,
            NetCashPosition = netCashPosition,
            RevenueGrowth = revenueGrowth,
            InvoiceCount = invoiceCount,
            BillCount = billCount,
            PeriodLabel = now.ToString("MMMM yyyy")
        };
    }

    /// <summary>
    /// Operational metrics for system admin — pending items, health indicators, data quality.
    /// Used by admin dashboard widgets to surface action items.
    /// </summary>
    public async Task<OperationalMetricsDto> GetOperationalMetricsAsync(Guid companyId)
    {
        var now = DateTime.UtcNow.Date;
        var metrics = new OperationalMetricsDto();

        // Draft documents needing attention
        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();

        metrics.DraftDocuments =
            siQuery.Count(x => x.CompanyId == companyId && x.Status == DocumentStatus.Draft) +
            piQuery.Count(x => x.CompanyId == companyId && x.Status == DocumentStatus.Draft);

        // Overdue invoices (posted, outstanding > 0, past due)
        metrics.OverdueInvoices = siQuery.Count(x =>
            x.CompanyId == companyId
            && x.Status == DocumentStatus.Posted
            && (x.GrandTotal - x.AmountPaid) > 0
            && x.DueDate < now);

        // AR/AP outstanding totals
        metrics.TotalArOutstanding = siQuery
            .Where(x => x.CompanyId == companyId && x.Status == DocumentStatus.Posted && !x.IsReturn)
            .Sum(x => x.GrandTotal - x.AmountPaid);

        metrics.TotalApOutstanding = piQuery
            .Where(x => x.CompanyId == companyId && x.Status == DocumentStatus.Posted && !x.IsReturn)
            .Sum(x => x.GrandTotal - x.AmountPaid);

        // Oldest unpaid invoice
        var oldestUnpaid = siQuery
            .Where(x => x.CompanyId == companyId && x.Status == DocumentStatus.Posted && (x.GrandTotal - x.AmountPaid) > 0)
            .OrderBy(x => x.DueDate)
            .FirstOrDefault();
        if (oldestUnpaid?.DueDate != null)
            metrics.OldestUnpaidInvoiceDays = (decimal)(now - oldestUnpaid.DueDate.Value).TotalDays;

        // Low stock items (from existing method logic)
        try
        {
            var lowStock = await GetLowStockItemsAsync();
            metrics.LowStockItems = lowStock?.Count ?? 0;
        }
        catch (Exception ex) { Logger.LogWarning(ex, "Low stock query failed"); metrics.LowStockItems = 0; }

        return metrics;
    }

    /// <summary>
    /// Returns total stock valuation summary for the company — used by dashboard widget.
    /// Shows total inventory value, item count, and top items by value.
    /// </summary>
    public async Task<StockValuationWidgetDto> GetStockValuationSummaryAsync(Guid companyId)
    {
        var binQuery = await _binRepo.GetQueryableAsync();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouseQuery = await warehouseRepo.GetQueryableAsync();

        // Get warehouse IDs for the company
        var companyWarehouseIds = warehouseQuery
            .Where(w => w.CompanyId == companyId && !w.IsGroup)
            .Select(w => w.Id).ToList();

        var bins = binQuery
            .Where(b => companyWarehouseIds.Contains(b.WarehouseId) && b.ActualQty > 0)
            .ToList();

        var totalValue = bins.Sum(b => b.ActualQty * b.ValuationRate);
        var totalItems = bins.Select(b => b.ItemId).Distinct().Count();
        var totalQty = bins.Sum(b => b.ActualQty);

        // Top 5 items by stock value
        var itemIds = bins.Select(b => b.ItemId).Distinct().ToList();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id);

        var topItems = bins
            .GroupBy(b => b.ItemId)
            .Select(g => new StockValuationItemDto
            {
                ItemId = g.Key,
                ItemCode = itemNames.TryGetValue(g.Key, out var info) ? info.ItemCode : "—",
                ItemName = itemNames.TryGetValue(g.Key, out var info2) ? info2.ItemName : "—",
                Quantity = g.Sum(b => b.ActualQty),
                ValuationRate = g.Average(b => b.ValuationRate),
                StockValue = g.Sum(b => b.ActualQty * b.ValuationRate)
            })
            .OrderByDescending(i => i.StockValue)
            .Take(5)
            .ToList();

        return new StockValuationWidgetDto
        {
            TotalStockValue = totalValue,
            TotalItems = totalItems,
            TotalQuantity = totalQty,
            TopItemsByValue = topItems
        };
    }
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
