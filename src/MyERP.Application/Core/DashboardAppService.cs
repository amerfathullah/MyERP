using System;
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
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

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
            MonthlyRevenue = (await _salesInvoiceRepo.GetListAsync(i => i.Status == DocumentStatus.Posted && i.IssueDate >= monthStart))
                .Sum(i => i.GrandTotal),
            MonthlyExpenses = (await _purchaseInvoiceRepo.GetListAsync(i => i.Status == DocumentStatus.Posted && i.IssueDate >= monthStart))
                .Sum(i => i.GrandTotal),
        };
    }

    /// <summary>
    /// Returns items whose projected qty is at or below their reorder level.
    /// Used by the dashboard low-stock alert widget.
    /// </summary>
    public async Task<List<LowStockItemDto>> GetLowStockItemsAsync()
    {
        var items = await _itemRepo.GetListAsync(i => i.ReorderLevel > 0 && i.IsActive);
        var bins = await _binRepo.GetQueryableAsync();

        var result = new List<LowStockItemDto>();
        foreach (var item in items)
        {
            var itemBins = bins.Where(b => b.ItemId == item.Id).ToList();
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
        var sixMonthsAgo = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-5);
        var invoices = await _salesInvoiceRepo.GetListAsync(
            i => i.Status == DocumentStatus.Posted && i.IssueDate >= sixMonthsAgo);

        var trend = invoices
            .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
            .Select(g => new RevenueTrendDto
            {
                Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                Amount = g.Sum(i => i.GrandTotal),
            })
            .OrderBy(x => x.Month)
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
}
