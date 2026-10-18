using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Implements Billed Items to be Received report matching ERPNext accounts/report/billed_items_to_be_received.
/// Finds purchase invoices where UpdateStock=false and items billed have not been fully received into warehouse stock.
/// </summary>
[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class BilledItemsToBeReceivedAppService : ApplicationService, IBilledItemsToBeReceivedAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public BilledItemsToBeReceivedAppService(
        IRepository<PurchaseInvoice, Guid> invoiceRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _invoiceRepo = invoiceRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _companyRepo = companyRepo;
    }

    public async Task<BilledItemsToBeReceivedReportDto> GetReportAsync(GetBilledItemsToBeReceivedInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";
        var cutoffDate = input.PostingDate ?? DateTime.UtcNow.Date;

        var piQuery = await _invoiceRepo.WithDetailsAsync(pi => pi.Items);
        var invoices = piQuery
            .Where(pi => pi.CompanyId == input.CompanyId
                         && pi.Status != DocumentStatus.Draft
                         && pi.Status != DocumentStatus.Cancelled
                         && pi.Status != DocumentStatus.Closed
                         && !pi.IsReturn
                         && !pi.IsDebitNote
                         && !pi.IsOpening
                         && !pi.UpdateStock
                         && pi.IssueDate <= cutoffDate)
            .WhereIf(input.PurchaseInvoiceId.HasValue, pi => pi.Id == input.PurchaseInvoiceId!.Value)
            .WhereIf(input.SupplierId.HasValue, pi => pi.SupplierId == input.SupplierId!.Value)
            .ToList();

        if (invoices.Count == 0)
        {
            return new BilledItemsToBeReceivedReportDto { Currency = companyCurrency };
        }

        var allItems = invoices
            .SelectMany(pi => pi.Items.Select(item => new { Invoice = pi, Item = item }))
            .Where(x => x.Item.Quantity > 0 && x.Item.Quantity > x.Item.ReceivedQty)
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .ToList();

        if (allItems.Count == 0)
        {
            return new BilledItemsToBeReceivedReportDto { Currency = companyCurrency };
        }

        var itemIds = allItems.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var itemsMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var supplierIds = invoices.Select(pi => pi.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepo.GetQueryableAsync();
        var suppliersMap = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var rows = new List<BilledItemToBeReceivedRowDto>();

        foreach (var line in allItems)
        {
            var pi = line.Invoice;
            var item = line.Item;

            itemsMap.TryGetValue(item.ItemId, out var itemMaster);

            // Filter item.is_stock_item == 1 per ERPNext billed_items_to_be_received
            if (itemMaster != null && !itemMaster.MaintainStock)
            {
                continue;
            }

            var conversionRate = pi.ExchangeRate > 0 ? pi.ExchangeRate : 1m;
            var invoicedQty = item.Quantity;
            var receivedQty = item.ReceivedQty;
            var pendingQty = invoicedQty - receivedQty;

            var amount = Math.Round(invoicedQty * item.UnitPrice * conversionRate, 2);
            var receivedAmount = Math.Round(receivedQty * item.UnitPrice * conversionRate, 2);
            var pendingAmount = Math.Round(pendingQty * item.UnitPrice * conversionRate, 2);

            suppliersMap.TryGetValue(pi.SupplierId, out var supplierName);

            rows.Add(new BilledItemToBeReceivedRowDto
            {
                PurchaseInvoiceId = pi.Id,
                InvoiceNumber = pi.InvoiceNumber,
                PostingDate = pi.IssueDate,
                SupplierId = pi.SupplierId,
                SupplierName = supplierName ?? string.Empty,
                ItemId = item.ItemId,
                ItemCode = itemMaster?.ItemCode ?? string.Empty,
                ItemName = itemMaster?.ItemName ?? string.Empty,
                Description = item.Description ?? string.Empty,
                Uom = item.Uom ?? "Unit",
                InvoicedQty = invoicedQty,
                ReceivedQty = receivedQty,
                PendingQty = pendingQty,
                Rate = item.UnitPrice,
                Amount = amount,
                ReceivedAmount = receivedAmount,
                PendingAmount = pendingAmount,
                Currency = companyCurrency
            });
        }

        return new BilledItemsToBeReceivedReportDto
        {
            Rows = rows.OrderBy(r => r.PostingDate).ThenBy(r => r.InvoiceNumber).ToList(),
            TotalAmount = Math.Round(rows.Sum(r => r.Amount), 2),
            TotalReceivedAmount = Math.Round(rows.Sum(r => r.ReceivedAmount), 2),
            TotalPendingAmount = Math.Round(rows.Sum(r => r.PendingAmount), 2),
            Currency = companyCurrency
        };
    }
}
