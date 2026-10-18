using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Implements Received Items to be Billed report matching ERPNext accounts/report/received_items_to_be_billed
/// and non_billed_report.py.
/// </summary>
[Authorize(MyERPPermissions.PurchaseReceipts.Default)]
public class ReceivedItemsToBeBilledAppService : ApplicationService, IReceivedItemsToBeBilledAppService
{
    private readonly IRepository<PurchaseReceipt, Guid> _receiptRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo;
    private readonly IRepository<Project, Guid> _projectRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public ReceivedItemsToBeBilledAppService(
        IRepository<PurchaseReceipt, Guid> receiptRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<PurchaseOrder, Guid> purchaseOrderRepo,
        IRepository<Project, Guid> projectRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _receiptRepo = receiptRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _purchaseOrderRepo = purchaseOrderRepo;
        _projectRepo = projectRepo;
        _companyRepo = companyRepo;
    }

    /// <inheritdoc />
    public async Task<ReceivedItemsToBeBilledReportDto> GetReportAsync(GetReceivedItemsToBeBilledInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";
        var cutoffDate = input.PostingDate ?? DateTime.UtcNow.Date;

        var prQuery = await _receiptRepo.WithDetailsAsync(r => r.Items);
        var receipts = prQuery
            .Where(r => r.CompanyId == input.CompanyId
                        && r.Status != DocumentStatus.Draft
                        && r.Status != DocumentStatus.Cancelled
                        && r.Status != DocumentStatus.Closed
                        && !r.IsReturn
                        && r.PostingDate <= cutoffDate)
            .WhereIf(input.PurchaseReceiptId.HasValue, r => r.Id == input.PurchaseReceiptId!.Value)
            .WhereIf(input.SupplierId.HasValue, r => r.SupplierId == input.SupplierId!.Value)
            .ToList();

        if (receipts.Count == 0)
        {
            return new ReceivedItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        // Exclude fully billed receipts per ERPNext status.notin(["Closed", "Completed"])
        receipts = receipts.Where(r => r.PerBilled < 100m).ToList();

        if (receipts.Count == 0)
        {
            return new ReceivedItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        var allItems = receipts
            .SelectMany(r => r.Items.Select(item => new { Receipt = r, Item = item }))
            .Where(x => !x.Item.IsClosed && x.Item.Quantity > 0)
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .ToList();

        if (allItems.Count == 0)
        {
            return new ReceivedItemsToBeBilledReportDto { Currency = companyCurrency };
        }

        var itemIds = allItems.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var itemsMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepo.GetQueryableAsync();
        var suppliersMap = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var poIds = receipts.Where(r => r.PurchaseOrderId.HasValue).Select(r => r.PurchaseOrderId!.Value).Distinct().ToList();
        var poMap = new Dictionary<Guid, Guid?>();
        if (poIds.Count > 0)
        {
            var poQuery = await _purchaseOrderRepo.GetQueryableAsync();
            poMap = poQuery.Where(p => poIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.ProjectId);
        }

        var projectIds = poMap.Values.Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
        var projectMap = new Dictionary<Guid, string>();
        if (projectIds.Count > 0)
        {
            var projQuery = await _projectRepo.GetQueryableAsync();
            projectMap = projQuery.Where(p => projectIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.ProjectName);
        }

        var rows = new List<ReceivedItemToBeBilledRowDto>();

        foreach (var line in allItems)
        {
            var pr = line.Receipt;
            var item = line.Item;

            itemsMap.TryGetValue(item.ItemId, out var itemMaster);

            // Filter item.is_stock_item == 1 per ERPNext non_billed_report.py
            if (itemMaster != null && !itemMaster.MaintainStock)
            {
                continue;
            }

            var conversionRate = pr.ExchangeRate > 0 ? pr.ExchangeRate : 1m;
            var amount = Math.Round(item.LineTotal * conversionRate, 2);
            var billedAmount = Math.Round(item.BilledQty * item.UnitPrice * conversionRate, 2);
            var returnedAmount = Math.Round(item.RejectedQty * item.UnitPrice * conversionRate, 2);
            var pendingAmount = Math.Round(amount - billedAmount - returnedAmount, 2);

            // ERPNext condition: pending_amount > 0
            if (pendingAmount <= 0)
            {
                continue;
            }

            suppliersMap.TryGetValue(pr.SupplierId, out var supplierName);

            Guid? projectId = null;
            string? projectName = null;
            if (pr.PurchaseOrderId.HasValue && poMap.TryGetValue(pr.PurchaseOrderId.Value, out var pId) && pId.HasValue)
            {
                projectId = pId.Value;
                projectMap.TryGetValue(projectId.Value, out projectName);
            }

            var pendingQty = item.PendingBillingQty;

            rows.Add(new ReceivedItemToBeBilledRowDto
            {
                PurchaseReceiptId = pr.Id,
                ReceiptNumber = pr.ReceiptNumber,
                PostingDate = pr.PostingDate,
                SupplierId = pr.SupplierId,
                SupplierName = supplierName ?? string.Empty,
                ItemId = item.ItemId,
                ItemCode = itemMaster?.ItemCode ?? string.Empty,
                ItemName = itemMaster?.ItemName ?? string.Empty,
                Description = item.Description ?? string.Empty,
                ReceivedQty = item.Quantity,
                BilledQty = item.BilledQty,
                ReturnedQty = item.RejectedQty,
                PendingQty = pendingQty,
                Rate = item.UnitPrice,
                Amount = amount,
                BilledAmount = billedAmount,
                ReturnedAmount = returnedAmount,
                PendingAmount = pendingAmount,
                ProjectId = projectId,
                ProjectName = projectName,
                Currency = companyCurrency
            });
        }

        // Order by PostingDate desc, ReceiptNumber desc matching ERPNext orderby
        rows = rows.OrderByDescending(r => r.PostingDate).ThenByDescending(r => r.ReceiptNumber).ToList();

        return new ReceivedItemsToBeBilledReportDto
        {
            Rows = rows,
            TotalAmount = rows.Sum(r => r.Amount),
            TotalBilledAmount = rows.Sum(r => r.BilledAmount),
            TotalReturnedAmount = rows.Sum(r => r.ReturnedAmount),
            TotalPendingAmount = rows.Sum(r => r.PendingAmount),
            Currency = companyCurrency
        };
    }
}
