using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.EInvoice.Entities;
using MyERP.Workflow;
using MyERP.Workflow.Entities;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize]
public class DashboardAppService : ApplicationService, IDashboardAppService
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
    /// Creates a Purchase Material Request from selected low-stock items.
    /// Reorder qty = ReorderLevel - ProjectedQty (brings stock back to reorder point).
    /// </summary>
    [Authorize(MyERPPermissions.MaterialRequests.Create)]
    public async Task<QuickReorderResultDto> CreateReorderMaterialRequestAsync(QuickReorderDto input)
    {
        if (input.ItemIds == null || input.ItemIds.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007");

        var items = await _itemRepo.GetListAsync(i => input.ItemIds.Contains(i.Id) && i.IsActive);
        if (!items.Any())
            throw new Volo.Abp.BusinessException("MyERP:01007");

        var binQuery = await _binRepo.GetQueryableAsync();
        var itemIds = items.Select(i => i.Id).ToHashSet();
        var bins = binQuery.Where(b => itemIds.Contains(b.ItemId)).ToList();
        var binsByItem = bins.GroupBy(b => b.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var numberGen = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();
        var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();

        var mrNumber = await numberGen.GenerateAsync("MR", input.CompanyId);
        var mr = new Purchasing.Entities.MaterialRequest(
            GuidGenerator.Create(), input.CompanyId, mrNumber,
            Purchasing.MaterialRequestType.Purchase, DateTime.UtcNow, CurrentTenant.Id)
        {
            Notes = "Auto-generated from low stock alert",
        };

        var itemCount = 0;
        foreach (var item in items)
        {
            var projectedQty = binsByItem.GetValueOrDefault(item.Id, new List<Bin>()).Sum(b => b.ProjectedQty);
            var reorderQty = Math.Max(1, item.ReorderLevel - (int)projectedQty);
            mr.AddItem(item.Id, item.ItemName, reorderQty, item.Uom ?? "Unit", null);
            itemCount++;
        }

        await mrRepo.InsertAsync(mr);

        return new QuickReorderResultDto
        {
            MaterialRequestId = mr.Id,
            MaterialRequestNumber = mrNumber,
            ItemCount = itemCount,
        };
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
    /// 6-month revenue vs expenses comparison for profitability-at-a-glance dashboard widget.
    /// Per ERPNext: finance managers need instant visibility into monthly profit margins.
    /// </summary>
    public async Task<List<RevenueVsExpenseDto>> GetRevenueVsExpenseTrendAsync()
    {
        var sixMonthsAgo = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();

        var revenueTrend = siQuery
            .Where(i => i.Status == DocumentStatus.Posted && !i.IsReturn && i.IssueDate >= sixMonthsAgo)
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new { Key = g.Key.Year + "-" + g.Key.Month.ToString().PadLeft(2, '0'), Amount = g.Sum(i => i.GrandTotal) })
            .ToList();

        var expenseTrend = piQuery
            .Where(i => i.Status == DocumentStatus.Posted && !i.IsReturn && i.IssueDate >= sixMonthsAgo)
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new { Key = g.Key.Year + "-" + g.Key.Month.ToString().PadLeft(2, '0'), Amount = g.Sum(i => i.GrandTotal) })
            .ToList();

        var result = new List<RevenueVsExpenseDto>();
        for (int i = 0; i < 6; i++)
        {
            var d = sixMonthsAgo.AddMonths(i);
            var key = $"{d.Year}-{d.Month:D2}";
            result.Add(new RevenueVsExpenseDto
            {
                Month = key,
                Revenue = revenueTrend.FirstOrDefault(t => t.Key == key)?.Amount ?? 0,
                Expenses = expenseTrend.FirstOrDefault(t => t.Key == key)?.Amount ?? 0,
            });
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
        var monthlyRevenue = monthSiQuery.Select(si => si.GrandTotal).Sum();
        var invoiceCount = monthSiQuery.Count();

        // Current month expenses
        var monthPiQuery = piQuery.Where(pi =>
            pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted &&
            pi.IssueDate >= monthStart && pi.IssueDate <= monthEnd && !pi.IsReturn);
        var monthlyExpenses = monthPiQuery.Select(pi => pi.GrandTotal).Sum();
        var billCount = monthPiQuery.Count();

        var netProfit = monthlyRevenue - monthlyExpenses;

        // AR/AP outstanding — server-side sum
        var arOutstanding = siQuery.Where(si =>
            si.CompanyId == companyId && si.Status == DocumentStatus.Posted && !si.IsReturn)
            .Select(si => si.GrandTotal - si.AmountPaid).Sum();

        var apOutstanding = piQuery.Where(pi =>
            pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted && !pi.IsReturn)
            .Select(pi => pi.GrandTotal - pi.AmountPaid).Sum();

        var netCashPosition = arOutstanding - apOutstanding;

        // Previous month revenue for growth calculation
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthEnd = monthStart.AddDays(-1);
        var prevMonthRevenue = siQuery.Where(si =>
            si.CompanyId == companyId && si.Status == DocumentStatus.Posted &&
            si.IssueDate >= prevMonthStart && si.IssueDate <= prevMonthEnd && !si.IsReturn)
            .Select(si => si.GrandTotal).Sum();

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

    /// <summary>
    /// Returns overdue invoice alerts for the dashboard banner.
    /// Shows count + amount of overdue receivables and payables, plus pending approval count.
    /// </summary>
    public async Task<OverdueAlertsDto> GetOverdueAlertsAsync(Guid companyId)
    {
        var now = DateTime.UtcNow;

        // Overdue receivables (SI posted, outstanding > 0, due date < today)
        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var overdueReceivables = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         !si.IsReturn &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0 &&
                         si.DueDate.HasValue && si.DueDate.Value < now)
            .Select(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance)
            .ToList();

        // Overdue payables (PI posted, outstanding > 0, due date < today)
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();
        var overduePayables = piQuery
            .Where(pi => pi.CompanyId == companyId &&
                         pi.Status == DocumentStatus.Posted &&
                         !pi.IsReturn &&
                         (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0 &&
                         pi.DueDate.HasValue && pi.DueDate.Value < now)
            .Select(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance)
            .ToList();

        // Pending approvals
        var approvalRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ApprovalRequest, Guid>>();
        var approvalQuery = await approvalRepo.GetQueryableAsync();
        var pendingApprovals = approvalQuery
            .Count(ar => ar.Status == MyERP.Workflow.ApprovalStatus.Pending);

        // Overdue purchase orders (active POs past expected delivery date)
        var poQuery = await _purchaseOrderRepo.GetQueryableAsync();
        var overduePOs = poQuery
            .Count(po => po.CompanyId == companyId &&
                         po.Status != DocumentStatus.Draft &&
                         po.Status != DocumentStatus.Cancelled &&
                         po.Status != DocumentStatus.Completed &&
                         po.ExpectedDeliveryDate.HasValue &&
                         po.ExpectedDeliveryDate.Value < now);

        return new OverdueAlertsDto
        {
            OverdueReceivableCount = overdueReceivables.Count,
            OverdueReceivableAmount = overdueReceivables.Sum(),
            OverduePayableCount = overduePayables.Count,
            OverduePayableAmount = overduePayables.Sum(),
            PendingApprovalCount = pendingApprovals,
            OverduePurchaseOrderCount = overduePOs,
        };
    }

    public async Task<TodaysActivityDto> GetTodaysActivityAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var siQ = await _salesInvoiceRepo.GetQueryableAsync();
        var invoicesToday = siQ.Where(i => i.CompanyId == companyId && i.CreationTime >= today && i.CreationTime < tomorrow && !i.IsReturn);
        var invoiceCount = invoicesToday.Count();
        var totalInvoiced = invoicesToday.Where(i => i.Status == DocumentStatus.Posted).Sum(i => i.GrandTotal);

        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.PaymentEntry, Guid>>();
        var peQ = await peRepo.GetQueryableAsync();
        var paymentsToday = peQ.Where(p => p.CompanyId == companyId && p.CreationTime >= today && p.CreationTime < tomorrow);
        var paymentCount = paymentsToday.Count();
        var totalCollected = paymentsToday.Where(p => p.Status == DocumentStatus.Posted && p.PaymentType == MyERP.Accounting.PaymentType.Receive).Sum(p => p.PaidAmount);

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        var soQ = await soRepo.GetQueryableAsync();
        var ordersToday = soQ.Count(o => o.CompanyId == companyId && o.CreationTime >= today && o.CreationTime < tomorrow);

        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>>();
        var dnQ = await dnRepo.GetQueryableAsync();
        var deliveriesToday = dnQ.Count(d => d.CompanyId == companyId && d.CreationTime >= today && d.CreationTime < tomorrow && !d.IsReturn);

        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseReceipt, Guid>>();
        var prQ = await prRepo.GetQueryableAsync();
        var receiptsToday = prQ.Count(r => r.CompanyId == companyId && r.CreationTime >= today && r.CreationTime < tomorrow && !r.IsReturn);

        return new TodaysActivityDto
        {
            InvoicesCreated = invoiceCount,
            PaymentsReceived = paymentCount,
            OrdersPlaced = ordersToday,
            DeliveriesMade = deliveriesToday,
            ReceiptsProcessed = receiptsToday,
            TotalInvoiced = totalInvoiced,
            TotalCollected = totalCollected,
        };
    }

    public async Task<List<PendingMaterialRequestDto>> GetPendingMaterialRequestsAsync(Guid companyId)
    {
        var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.MaterialRequest, Guid>>();
        var mrQuery = await mrRepo.GetQueryableAsync();

        var recentMrs = mrQuery
            .Where(mr => mr.CompanyId == companyId &&
                         (mr.Status == DocumentStatus.Draft || mr.Status == DocumentStatus.Submitted) &&
                         mr.RequestType == MyERP.Purchasing.MaterialRequestType.Purchase)
            .OrderByDescending(mr => mr.CreationTime)
            .Take(10)
            .Select(mr => new PendingMaterialRequestDto
            {
                Id = mr.Id,
                RequestNumber = mr.RequestNumber ?? "—",
                RequestDate = mr.RequestDate,
                Status = mr.Status,
                ItemCount = mr.Items.Count,
                RequiredByDate = mr.RequiredByDate,
            })
            .ToList();

        return recentMrs;
    }

    /// <summary>
    /// Returns bank and cash account balances for the dashboard cash position widget.
    /// </summary>
    public async Task<BankBalanceWidgetDto> GetBankBalancesAsync(Guid companyId)
    {
        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var jeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Accounting.Entities.JournalEntry, Guid>>();

        var accountQuery = await accountRepo.GetQueryableAsync();
        var bankCashAccounts = accountQuery
            .Where(a => a.CompanyId == companyId && a.IsActive &&
                        (a.AccountSubType == MyERP.Accounting.AccountSubType.BankAccount ||
                         a.AccountSubType == MyERP.Accounting.AccountSubType.CashAccount))
            .Select(a => new { a.Id, a.AccountName, a.AccountCode, a.AccountSubType })
            .ToList();

        if (!bankCashAccounts.Any())
            return new BankBalanceWidgetDto();

        var accountIds = bankCashAccounts.Select(a => a.Id).ToHashSet();

        // Calculate balance per account from GL lines (debit - credit)
        var jeQuery = await jeRepo.GetQueryableAsync();
        var lineBalances = jeQuery
            .Where(je => je.CompanyId == companyId && je.Status == DocumentStatus.Posted)
            .SelectMany(je => je.Lines)
            .Where(l => accountIds.Contains(l.AccountId))
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(l => l.IsDebit ? l.Amount : -l.Amount) })
            .ToList();

        var balanceMap = lineBalances.ToDictionary(b => b.AccountId, b => b.Balance);

        var accounts = bankCashAccounts.Select(a => new BankAccountBalanceDto
        {
            AccountName = a.AccountName,
            AccountCode = a.AccountCode,
            Balance = balanceMap.GetValueOrDefault(a.Id, 0),
            AccountType = a.AccountSubType == MyERP.Accounting.AccountSubType.CashAccount ? "Cash" : "Bank",
        })
        .OrderByDescending(a => a.Balance)
        .ToList();

        return new BankBalanceWidgetDto
        {
            TotalCashAndBank = accounts.Sum(a => a.Balance),
            Accounts = accounts,
        };
    }

    /// <summary>
    /// Returns aging bucket summary for the dashboard widget.
    /// Shows receivable and payable amounts grouped by 0-30, 31-60, 61-90, 91+ days overdue.
    /// </summary>
    public async Task<AgingSummaryWidgetDto> GetAgingSummaryWidgetAsync(Guid companyId)
    {
        var today = DateTime.UtcNow;

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var outstandingSi = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         !si.IsReturn &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0)
            .Select(si => new { OutstandingAmount = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance, si.DueDate })
            .ToList();

        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();
        var outstandingPi = piQuery
            .Where(pi => pi.CompanyId == companyId &&
                         pi.Status == DocumentStatus.Posted &&
                         !pi.IsReturn &&
                         (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0)
            .Select(pi => new { OutstandingAmount = pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance, pi.DueDate })
            .ToList();

        var receivableBuckets = new decimal[4]; // 0-30, 31-60, 61-90, 91+
        foreach (var si in outstandingSi)
        {
            var daysOverdue = si.DueDate.HasValue ? Math.Max(0, (int)(today - si.DueDate.Value).TotalDays) : 0;
            var idx = daysOverdue switch { <= 30 => 0, <= 60 => 1, <= 90 => 2, _ => 3 };
            receivableBuckets[idx] += si.OutstandingAmount;
        }

        var payableBuckets = new decimal[4];
        foreach (var pi in outstandingPi)
        {
            var daysOverdue = pi.DueDate.HasValue ? Math.Max(0, (int)(today - pi.DueDate.Value).TotalDays) : 0;
            var idx = daysOverdue switch { <= 30 => 0, <= 60 => 1, <= 90 => 2, _ => 3 };
            payableBuckets[idx] += pi.OutstandingAmount;
        }

        return new AgingSummaryWidgetDto
        {
            Receivables = new AgingBucketsDto
            {
                Current = receivableBuckets[0],
                ThirtyOneToSixty = receivableBuckets[1],
                SixtyOneToNinety = receivableBuckets[2],
                NinetyPlus = receivableBuckets[3],
                Total = receivableBuckets.Sum(),
            },
            Payables = new AgingBucketsDto
            {
                Current = payableBuckets[0],
                ThirtyOneToSixty = payableBuckets[1],
                SixtyOneToNinety = payableBuckets[2],
                NinetyPlus = payableBuckets[3],
                Total = payableBuckets.Sum(),
            },
        };
    }

    /// <summary>
    /// Returns a 30-day cash flow snapshot for the dashboard.
    /// Shows expected inflows (from SI due dates) vs outflows (from PI due dates) for the next 30 days,
    /// enabling quick assessment of upcoming cash position.
    /// Per ERPNext: Cash Flow Forecast uses invoice DueDate for projection.
    /// </summary>
    public async Task<CashFlowSnapshotDto> GetCashFlowSnapshotAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var thirtyDaysAhead = today.AddDays(30);

        // Expected inflows: outstanding SI due within next 30 days
        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var upcomingReceivables = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         !si.IsReturn &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0 &&
                         si.DueDate.HasValue &&
                         si.DueDate.Value >= today &&
                         si.DueDate.Value <= thirtyDaysAhead)
            .Select(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance)
            .ToList();

        // Expected outflows: outstanding PI due within next 30 days
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();
        var upcomingPayables = piQuery
            .Where(pi => pi.CompanyId == companyId &&
                         pi.Status == DocumentStatus.Posted &&
                         !pi.IsReturn &&
                         (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0 &&
                         pi.DueDate.HasValue &&
                         pi.DueDate.Value >= today &&
                         pi.DueDate.Value <= thirtyDaysAhead)
            .Select(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance)
            .ToList();

        // Overdue amounts (already past due date but still outstanding)
        var overdueReceivables = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         !si.IsReturn &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0 &&
                         si.DueDate.HasValue &&
                         si.DueDate.Value < today)
            .Select(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance)
            .ToList();

        var overduePayables = piQuery
            .Where(pi => pi.CompanyId == companyId &&
                         pi.Status == DocumentStatus.Posted &&
                         !pi.IsReturn &&
                         (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0 &&
                         pi.DueDate.HasValue &&
                         pi.DueDate.Value < today)
            .Select(pi => pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance)
            .ToList();

        var totalInflows = upcomingReceivables.Sum();
        var totalOutflows = upcomingPayables.Sum();

        return new CashFlowSnapshotDto
        {
            ExpectedInflows30Days = totalInflows,
            ExpectedOutflows30Days = totalOutflows,
            NetCashFlow30Days = totalInflows - totalOutflows,
            InflowInvoiceCount = upcomingReceivables.Count,
            OutflowInvoiceCount = upcomingPayables.Count,
            OverdueReceivables = overdueReceivables.Sum(),
            OverduePayables = overduePayables.Sum(),
            OverdueReceivableCount = overdueReceivables.Count,
            OverduePayableCount = overduePayables.Count,
        };
    }

    /// <summary>
    /// Returns quotations expiring within the next N days (default 7) for the sales pipeline widget.
    /// Per ERPNext: quotation list shows validity status for collections management.
    /// </summary>
    public async Task<List<ExpiringQuotationDto>> GetExpiringQuotationsAsync(Guid companyId, int daysAhead = 7)
    {
        var quotationRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.Quotation, Guid>>();
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();

        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(daysAhead);

        var qQuery = await quotationRepo.GetQueryableAsync();
        var expiring = qQuery
            .Where(q => q.CompanyId == companyId &&
                        q.Status == DocumentStatus.Submitted &&
                        q.ValidUntil.HasValue &&
                        q.ValidUntil.Value >= today &&
                        q.ValidUntil.Value <= cutoff)
            .OrderBy(q => q.ValidUntil)
            .Take(20)
            .ToList();

        if (expiring.Count == 0) return new List<ExpiringQuotationDto>();

        // Resolve customer names
        var customerIds = expiring.Select(q => q.CustomerId).Distinct().ToList();
        var custQuery = await customerRepo.GetQueryableAsync();
        var customers = custQuery.Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        return expiring.Select(q => new ExpiringQuotationDto
        {
            QuotationId = q.Id,
            QuotationNumber = q.QuotationNumber,
            CustomerName = customers.GetValueOrDefault(q.CustomerId) ?? "—",
            GrandTotal = q.GrandTotal,
            ValidUntil = q.ValidUntil!.Value,
            DaysRemaining = (int)(q.ValidUntil!.Value - today).TotalDays,
        }).ToList();
    }

    /// <summary>
    /// Returns top 5 customers ranked by revenue for the current month.
    /// Per ERPNext: Customer Acquisition report shows revenue ranking for sales management.
    /// </summary>
    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(Guid companyId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var customerQuery = await _customerRepo.GetQueryableAsync();

        // Group posted SI by customer for this month (exclude returns)
        var customerRevenue = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         si.IssueDate >= monthStart &&
                         !si.IsReturn)
            .GroupBy(si => si.CustomerId)
            .Select(g => new { CustomerId = g.Key, Revenue = g.Sum(si => si.GrandTotal), InvoiceCount = g.Count() })
            .OrderByDescending(g => g.Revenue)
            .Take(5)
            .ToList();

        if (!customerRevenue.Any()) return new List<TopCustomerDto>();

        var customerIds = customerRevenue.Select(c => c.CustomerId).ToHashSet();
        var customerNames = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToList()
            .ToDictionary(c => c.Id, c => c.Name);

        return customerRevenue.Select(c => new TopCustomerDto
        {
            CustomerId = c.CustomerId,
            CustomerName = customerNames.GetValueOrDefault(c.CustomerId) ?? "—",
            Revenue = c.Revenue,
            InvoiceCount = c.InvoiceCount,
        }).ToList();
    }

    /// <summary>
    /// Returns pending order counts by status for SO and PO pipelines.
    /// Per ERPNext: dashboard shows order pipeline for operations visibility.
    /// </summary>
    public async Task<PendingOrdersSummaryDto> GetPendingOrdersSummaryAsync(Guid companyId)
    {
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var poQuery = await _purchaseOrderRepo.GetQueryableAsync();

        var soToDeliverAndBill = soQuery.Count(s => s.CompanyId == companyId && s.Status == DocumentStatus.ToDeliverAndBill);
        var soToDeliver = soQuery.Count(s => s.CompanyId == companyId && s.Status == DocumentStatus.ToDeliver);
        var soToBill = soQuery.Count(s => s.CompanyId == companyId && s.Status == DocumentStatus.ToBill);

        var poToReceiveAndBill = poQuery.Count(p => p.CompanyId == companyId && p.Status == DocumentStatus.ToDeliverAndBill);
        var poToReceive = poQuery.Count(p => p.CompanyId == companyId && p.Status == DocumentStatus.ToDeliver);
        var poToBill = poQuery.Count(p => p.CompanyId == companyId && p.Status == DocumentStatus.ToBill);

        return new PendingOrdersSummaryDto
        {
            SalesOrdersToDeliverAndBill = soToDeliverAndBill,
            SalesOrdersToDeliver = soToDeliver,
            SalesOrdersToBill = soToBill,
            TotalActiveSalesOrders = soToDeliverAndBill + soToDeliver + soToBill,
            PurchaseOrdersToReceiveAndBill = poToReceiveAndBill,
            PurchaseOrdersToReceive = poToReceive,
            PurchaseOrdersToBill = poToBill,
            TotalActivePurchaseOrders = poToReceiveAndBill + poToReceive + poToBill,
        };
    }

    /// <summary>
    /// Returns production summary — work orders grouped by status for manufacturing visibility.
    /// Per ERPNext: manufacturing dashboard shows WO pipeline counts.
    /// </summary>
    public async Task<ProductionSummaryDto> GetProductionSummaryAsync(Guid companyId)
    {
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Manufacturing.Entities.WorkOrder, Guid>>();
        var woQuery = await woRepo.GetQueryableAsync();

        var companyWos = woQuery.Where(w => w.CompanyId == companyId);

        return new ProductionSummaryDto
        {
            Draft = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.Draft),
            NotStarted = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.Submitted || w.Status == Manufacturing.WorkOrderStatus.NotStarted),
            InProcess = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.InProcess),
            Completed = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.Completed),
            Stopped = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.Stopped),
            TotalActiveOrders = companyWos.Count(w => w.Status == Manufacturing.WorkOrderStatus.Submitted || w.Status == Manufacturing.WorkOrderStatus.NotStarted || w.Status == Manufacturing.WorkOrderStatus.InProcess || w.Status == Manufacturing.WorkOrderStatus.Stopped),
            TotalProducedThisMonth = companyWos
                .Where(w => w.Status == Manufacturing.WorkOrderStatus.Completed && w.PlannedStartDate >= new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1))
                .Sum(w => w.ProducedQuantity),
        };
    }

    /// <summary>
    /// Returns batches expiring within the next N days (default 30).
    /// Per ERPNext batch-serial-number: prevents shipping expired stock (compliance-critical).
    /// </summary>
    public async Task<List<ExpiringBatchDto>> GetExpiringBatchesAsync(Guid companyId, int daysAhead = 30)
    {
        var batchRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Inventory.Entities.Batch, Guid>>();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();

        var today = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(daysAhead);

        var batchQuery = await batchRepo.GetQueryableAsync();
        var expiringBatches = batchQuery
            .Where(b => b.ExpiryDate.HasValue && b.ExpiryDate.Value >= today && b.ExpiryDate.Value <= cutoff && !b.IsDisabled)
            .OrderBy(b => b.ExpiryDate)
            .Take(50)
            .ToList();

        if (!expiringBatches.Any()) return new List<ExpiringBatchDto>();

        var itemIds = expiringBatches.Select(b => b.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id);

        // Get stock per batch from Bin (aggregate across warehouses)
        var binQuery = await _binRepo.GetQueryableAsync();
        var warehouseQuery = await warehouseRepo.GetQueryableAsync();
        var companyWhIds = warehouseQuery.Where(w => w.CompanyId == companyId).Select(w => w.Id).ToHashSet();
        var batchItemIds = expiringBatches.Select(b => b.ItemId).ToHashSet();
        var stockByItem = binQuery
            .Where(b => companyWhIds.Contains(b.WarehouseId) && batchItemIds.Contains(b.ItemId) && b.ActualQty > 0)
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        return expiringBatches.Select(b => new ExpiringBatchDto
        {
            BatchId = b.Id,
            BatchNo = b.BatchNo,
            ItemCode = items.TryGetValue(b.ItemId, out var info) ? info.ItemCode : "—",
            ItemName = items.TryGetValue(b.ItemId, out var info2) ? info2.ItemName : "—",
            ExpiryDate = b.ExpiryDate!.Value,
            DaysUntilExpiry = (int)(b.ExpiryDate!.Value - today).TotalDays,
            StockQty = stockByItem.GetValueOrDefault(b.ItemId, 0),
        }).ToList();
    }

    /// <summary>
    /// Top 5 customers by outstanding amount — collections management priority list.
    /// Per ERPNext: Accounts Receivable Summary shows customers ranked by total outstanding.
    /// </summary>
    public async Task<List<TopDebtorDto>> GetTopDebtorsAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var customerQuery = await _customerRepo.GetQueryableAsync();

        var debtors = siQuery
            .Where(si => si.CompanyId == companyId &&
                         si.Status == DocumentStatus.Posted &&
                         !si.IsReturn &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
            .GroupBy(si => si.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalOutstanding = g.Sum(si => si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance),
                InvoiceCount = g.Count(),
                OldestDueDate = g.Min(si => si.DueDate),
            })
            .OrderByDescending(g => g.TotalOutstanding)
            .Take(5)
            .ToList();

        if (!debtors.Any()) return new List<TopDebtorDto>();

        var customerIds = debtors.Select(d => d.CustomerId).ToList();
        var names = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionary(c => c.Id, c => c.Name);

        return debtors.Select(d => new TopDebtorDto
        {
            CustomerId = d.CustomerId,
            CustomerName = names.GetValueOrDefault(d.CustomerId, "—"),
            TotalOutstanding = d.TotalOutstanding,
            InvoiceCount = d.InvoiceCount,
            OldestDueDate = d.OldestDueDate,
            DaysOverdue = d.OldestDueDate.HasValue && d.OldestDueDate.Value < today
                ? (int)(today - d.OldestDueDate.Value).TotalDays : 0,
        }).ToList();
    }

    public async Task<UpcomingPaymentDuesDto> GetUpcomingPaymentDuesAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var in7 = today.AddDays(7);
        var in14 = today.AddDays(14);
        var in30 = today.AddDays(30);

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var piQuery = await _purchaseInvoiceRepo.GetQueryableAsync();

        var outstandingSi = siQuery
            .Where(si => si.CompanyId == companyId && si.Status == DocumentStatus.Posted &&
                         !si.IsReturn && si.DueDate.HasValue &&
                         (si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance) > 0.01m)
            .Select(si => new { si.DueDate, Outstanding = si.GrandTotal - si.AmountPaid - si.WriteOffAmount - si.TotalAdvance })
            .ToList();

        var outstandingPi = piQuery
            .Where(pi => pi.CompanyId == companyId && pi.Status == DocumentStatus.Posted &&
                         !pi.IsReturn && pi.DueDate.HasValue &&
                         (pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance) > 0.01m)
            .Select(pi => new { pi.DueDate, Outstanding = pi.GrandTotal - pi.AmountPaid - pi.WriteOffAmount - pi.TotalAdvance })
            .ToList();

        return new UpcomingPaymentDuesDto
        {
            ReceivablesDueIn7Days = outstandingSi.Where(s => s.DueDate >= today && s.DueDate <= in7).Sum(s => s.Outstanding),
            ReceivablesDueIn14Days = outstandingSi.Where(s => s.DueDate >= today && s.DueDate <= in14).Sum(s => s.Outstanding),
            ReceivablesDueIn30Days = outstandingSi.Where(s => s.DueDate >= today && s.DueDate <= in30).Sum(s => s.Outstanding),
            ReceivablesOverdue = outstandingSi.Where(s => s.DueDate < today).Sum(s => s.Outstanding),
            PayablesDueIn7Days = outstandingPi.Where(p => p.DueDate >= today && p.DueDate <= in7).Sum(p => p.Outstanding),
            PayablesDueIn14Days = outstandingPi.Where(p => p.DueDate >= today && p.DueDate <= in14).Sum(p => p.Outstanding),
            PayablesDueIn30Days = outstandingPi.Where(p => p.DueDate >= today && p.DueDate <= in30).Sum(p => p.Outstanding),
            PayablesOverdue = outstandingPi.Where(p => p.DueDate < today).Sum(p => p.Outstanding),
            ReceivableInvoiceCount = outstandingSi.Count(s => s.DueDate >= today && s.DueDate <= in30),
            PayableInvoiceCount = outstandingPi.Count(p => p.DueDate >= today && p.DueDate <= in30),
        };
    }
    public async Task<List<ProfitMarginTrendDto>> GetProfitMarginTrendAsync(Guid companyId)
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        var result = new List<ProfitMarginTrendDto>();

        var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
        var invoices = siQuery
            .Where(si => si.CompanyId == companyId && si.Status == DocumentStatus.Posted && si.IssueDate >= sixMonthsAgo)
            .Select(si => new { si.IssueDate, si.NetTotal, Items = si.Items.Select(i => new { i.UnitPrice, i.ValuationRate, i.Quantity }) })
            .ToList();

        for (var i = 0; i < 6; i++)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-5 + i);
            var monthEnd = monthStart.AddMonths(1);
            var monthLabel = monthStart.ToString("MMM yyyy");

            var monthInvoices = invoices.Where(inv => inv.IssueDate >= monthStart && inv.IssueDate < monthEnd).ToList();
            var revenue = monthInvoices.Sum(inv => inv.NetTotal);
            var cost = monthInvoices.Sum(inv => inv.Items.Sum(item => item.ValuationRate * item.Quantity));
            var grossProfit = revenue - cost;
            var marginPct = revenue > 0 ? Math.Round(grossProfit / revenue * 100, 1) : 0;

            result.Add(new ProfitMarginTrendDto { Month = monthLabel, Revenue = revenue, Cost = cost, GrossProfit = grossProfit, MarginPercentage = marginPct });
        }

        return result;
    }

    /// <summary>
    /// PO delivery due date alerts — shows overdue and upcoming deliveries.
    /// Per ERPNext: critical for procurement follow-up with suppliers.
    /// </summary>
    public async Task<DeliveryDueAlertDto> GetDeliveryDueAlertsAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var in7Days = today.AddDays(7);

        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>();
        var poQuery = await poRepo.GetQueryableAsync();

        var activePOs = poQuery
            .Where(po => po.CompanyId == companyId &&
                         po.ExpectedDeliveryDate.HasValue &&
                         (po.Status == DocumentStatus.Submitted ||
                          po.Status == DocumentStatus.ToDeliverAndBill ||
                          po.Status == DocumentStatus.ToDeliver))
            .Select(po => new { po.Id, po.OrderNumber, po.SupplierId, po.ExpectedDeliveryDate, po.GrandTotal, po.Status })
            .ToList();

        var overdue = activePOs.Where(po => po.ExpectedDeliveryDate!.Value < today).ToList();
        var dueThisWeek = activePOs.Where(po => po.ExpectedDeliveryDate!.Value >= today && po.ExpectedDeliveryDate!.Value <= in7Days).ToList();

        var supplierIds = activePOs.Select(po => po.SupplierId).Distinct().ToList();
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
        var supplierQuery = await supplierRepo.GetQueryableAsync();
        var supplierNames = supplierQuery.Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name }).ToList().ToDictionary(s => s.Id, s => s.Name);

        return new DeliveryDueAlertDto
        {
            OverdueCount = overdue.Count,
            DueThisWeekCount = dueThisWeek.Count,
            DueNext7DaysCount = dueThisWeek.Count,
            OverdueTotalValue = overdue.Sum(po => po.GrandTotal),
            OverdueOrders = overdue.OrderBy(po => po.ExpectedDeliveryDate).Take(10).Select(po => new DeliveryDueOrderDto
            {
                PurchaseOrderId = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierName = supplierNames.GetValueOrDefault(po.SupplierId, "—"),
                ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                DaysOverdue = (int)(today - po.ExpectedDeliveryDate!.Value).TotalDays,
                GrandTotal = po.GrandTotal,
            }).ToList(),
            UpcomingOrders = dueThisWeek.OrderBy(po => po.ExpectedDeliveryDate).Take(10).Select(po => new DeliveryDueOrderDto
            {
                PurchaseOrderId = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierName = supplierNames.GetValueOrDefault(po.SupplierId, "—"),
                ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                DaysOverdue = 0,
                GrandTotal = po.GrandTotal,
            }).ToList(),
        };
    }

    /// <summary>
    /// Inventory reorder point dashboard — items at or below reorder level.
    /// Per ERPNext reorder_item: shows which items need procurement action.
    /// </summary>
    public async Task<ReorderPointDashboardDto> GetReorderPointDashboardAsync(Guid companyId)
    {
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var binQuery = await _binRepo.GetQueryableAsync();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouseQuery = await warehouseRepo.GetQueryableAsync();

        var companyWarehouseIds = warehouseQuery
            .Where(w => w.CompanyId == companyId && !w.IsGroup)
            .Select(w => w.Id).ToList();

        var reorderItems = itemQuery
            .Where(i => i.IsActive && i.MaintainStock && i.ReorderLevel > 0)
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.ReorderLevel, i.StandardBuyingPrice })
            .ToList();

        if (!reorderItems.Any()) return new ReorderPointDashboardDto();

        var itemIds = reorderItems.Select(i => i.Id).ToList();
        var bins = binQuery
            .Where(b => itemIds.Contains(b.ItemId) && companyWarehouseIds.Contains(b.WarehouseId))
            .ToList();

        var warehouseNames = warehouseQuery.Where(w => companyWarehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList().ToDictionary(w => w.Id, w => w.Name);

        var alerts = new List<ReorderPointItemDto>();
        foreach (var item in reorderItems)
        {
            var itemBins = bins.Where(b => b.ItemId == item.Id).ToList();
            var totalProjected = itemBins.Sum(b => b.ProjectedQty);

            if (totalProjected <= item.ReorderLevel)
            {
                var primaryBin = itemBins.OrderByDescending(b => b.ActualQty).FirstOrDefault();
                alerts.Add(new ReorderPointItemDto
                {
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName ?? "—",
                    CurrentStock = itemBins.Sum(b => b.ActualQty),
                    ReorderLevel = item.ReorderLevel,
                    ProjectedQty = totalProjected,
                    ShortageQty = Math.Max(0, item.ReorderLevel - totalProjected),
                    WarehouseName = primaryBin != null ? warehouseNames.GetValueOrDefault(primaryBin.WarehouseId, "—") : "—",
                });
            }
        }

        var sorted = alerts.OrderByDescending(a => a.ShortageQty).ToList();
        return new ReorderPointDashboardDto
        {
            TotalItemsBelowReorder = sorted.Count,
            CriticalItems = sorted.Count(a => a.ProjectedQty <= 0),
            TotalShortageValue = sorted.Sum(a => a.ShortageQty * (reorderItems.FirstOrDefault(i => i.Id == a.ItemId)?.StandardBuyingPrice ?? 0)),
            Items = sorted.Take(20).ToList(),
        };
    }

    /// <summary>
    /// Supplier on-time delivery performance KPIs for procurement dashboard.
    /// Per ERPNext supplier_scorecard: aggregates PO delivery metrics per supplier.
    /// Shows worst-performing suppliers requiring procurement follow-up.
    /// </summary>
    public async Task<SupplierPerformanceWidgetDto> GetSupplierPerformanceWidgetAsync(Guid companyId)
    {
        var today = DateTime.UtcNow.Date;
        var lookbackDate = today.AddMonths(-3);

        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.PurchaseOrder, Guid>>();
        var supplierRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();

        var poQuery = await poRepo.GetQueryableAsync();
        var completedPOs = poQuery
            .Where(po => po.CompanyId == companyId
                && po.ExpectedDeliveryDate.HasValue
                && po.OrderDate >= lookbackDate
                && po.Status != DocumentStatus.Draft
                && po.Status != DocumentStatus.Cancelled)
            .Select(po => new
            {
                po.SupplierId,
                po.ExpectedDeliveryDate,
                po.GrandTotal,
                IsFullyReceived = po.Status == DocumentStatus.ToBill || po.Status == DocumentStatus.Completed,
            })
            .ToList();

        if (!completedPOs.Any())
            return new SupplierPerformanceWidgetDto();

        var supplierIds = completedPOs.Select(po => po.SupplierId).Distinct().ToList();
        var supplierQuery = await supplierRepo.GetQueryableAsync();
        var supplierNames = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToList()
            .ToDictionary(s => s.Id, s => s.Name);

        var bySupplier = completedPOs.GroupBy(po => po.SupplierId).Select(g =>
        {
            var total = g.Count();
            var onTime = g.Count(po => po.IsFullyReceived && po.ExpectedDeliveryDate!.Value >= today);
            var late = g.Count(po => po.ExpectedDeliveryDate!.Value < today && !po.IsFullyReceived);
            var onTimeRate = total > 0 ? (decimal)onTime / total * 100 : 0m;

            return new SupplierPerformanceItemDto
            {
                SupplierId = g.Key,
                SupplierName = supplierNames.GetValueOrDefault(g.Key, "—"),
                TotalOrders = total,
                OnTimeCount = onTime,
                LateCount = late,
                OnTimeRate = Math.Round(onTimeRate, 1),
                TotalValue = g.Sum(po => po.GrandTotal),
            };
        })
        .OrderBy(s => s.OnTimeRate)
        .ToList();

        var overallOnTime = completedPOs.Count > 0
            ? Math.Round((decimal)completedPOs.Count(po => po.IsFullyReceived && po.ExpectedDeliveryDate!.Value >= today) / completedPOs.Count * 100, 1)
            : 0m;

        return new SupplierPerformanceWidgetDto
        {
            TotalSuppliers = bySupplier.Count,
            OverallOnTimeRate = overallOnTime,
            SuppliersAtRisk = bySupplier.Count(s => s.OnTimeRate < 80),
            Suppliers = bySupplier.Take(10).ToList(),
        };
    }
}

