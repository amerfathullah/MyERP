using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Purchase Order Analysis report application service.
/// Implements ERPNext buying/report/purchase_order_analysis with grouping by PO or by Item.
/// Per ERPNext: group_by_item groups by (company, item_code, uom).
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class PurchaseOrderAnalysisAppService : ApplicationService, IPurchaseOrderAnalysisAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PurchaseOrderAnalysisAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
    }

    public async Task<PurchaseOrderAnalysisReportDto> GetAnalysisAsync(GetPurchaseOrderAnalysisDto input)
    {
        if (input.GroupByPo && input.GroupByItem)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot combine Group By PO and Group By Item.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be greater than To Date.");
        }

        var fromDate = input.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var toDate = input.ToDate ?? DateTime.UtcNow.Date;

        var poQuery = await _purchaseOrderRepository.WithDetailsAsync(po => po.Items);
        var orders = poQuery
            .Where(po => po.CompanyId == input.CompanyId
                         && po.Status != DocumentStatus.Draft
                         && po.Status != DocumentStatus.Cancelled
                         && po.OrderDate >= fromDate
                         && po.OrderDate <= toDate)
            .WhereIf(input.SupplierId.HasValue, po => po.SupplierId == input.SupplierId!.Value)
            .WhereIf(input.PurchaseOrderId.HasValue, po => po.Id == input.PurchaseOrderId!.Value)
            .ToList();

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var supplierMap = (await _supplierRepository.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var allItemIds = orders.SelectMany(o => o.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        var warehouseIds = orders.SelectMany(o => o.Items)
            .Where(i => i.WarehouseId.HasValue)
            .Select(i => i.WarehouseId!.Value)
            .Distinct()
            .ToList();
        var warehouseMap = (await _warehouseRepository.GetQueryableAsync())
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var companyName = company?.Name ?? string.Empty;

        var detailedRows = new List<PurchaseOrderAnalysisRowDto>();

        foreach (var order in orders)
        {
            var isOrderClosed = order.Status == DocumentStatus.Closed;

            foreach (var item in order.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value)
                    continue;

                if (input.ProjectId.HasValue && item.ProjectId != input.ProjectId.Value)
                    continue;

                var isRowClosed = isOrderClosed || item.IsClosed;
                var itemInfo = itemMap.GetValueOrDefault(item.ItemId);
                var itemCode = itemInfo?.ItemCode ?? item.ItemId.ToString();
                var supplierName = supplierMap.GetValueOrDefault(order.SupplierId, string.Empty);
                var warehouseName = item.WarehouseId.HasValue
                    ? warehouseMap.GetValueOrDefault(item.WarehouseId.Value, string.Empty)
                    : string.Empty;

                var pendingQty = isRowClosed ? 0m : Math.Max(0m, item.Quantity - item.ReceivedQty);
                var qtyToBill = isRowClosed ? 0m : Math.Max(0m, item.Quantity - item.BilledQty);
                var amount = Math.Round(item.Quantity * item.UnitPrice * order.ExchangeRate, 2);
                var receivedQtyAmount = Math.Round(item.ReceivedQty * item.UnitPrice * order.ExchangeRate, 2);
                var billedAmount = Math.Round(item.BilledQty * item.UnitPrice * order.ExchangeRate, 2);
                var pendingAmount = isRowClosed ? 0m : Math.Round(Math.Max(0m, amount - billedAmount), 2);

                var requiredDate = item.ExpectedDeliveryDate ?? order.ExpectedDeliveryDate;
                var delayDays = 0;
                if (requiredDate.HasValue && requiredDate.Value.Date < DateTime.UtcNow.Date && pendingQty > 0)
                {
                    delayDays = (int)(DateTime.UtcNow.Date - requiredDate.Value.Date).TotalDays;
                }

                detailedRows.Add(new PurchaseOrderAnalysisRowDto
                {
                    PurchaseOrderNumber = order.OrderNumber,
                    PurchaseOrderId = order.Id,
                    Date = order.OrderDate,
                    RequiredDate = requiredDate,
                    ProjectId = item.ProjectId,
                    Status = order.Status.ToString(),
                    SupplierId = order.SupplierId,
                    SupplierName = supplierName,
                    ItemId = item.ItemId,
                    ItemCode = itemCode,
                    Description = item.Description,
                    Uom = item.Uom,
                    Qty = item.Quantity,
                    ReceivedQty = item.ReceivedQty,
                    PendingQty = pendingQty,
                    BilledQty = item.BilledQty,
                    QtyToBill = qtyToBill,
                    Amount = amount,
                    ReceivedQtyAmount = receivedQtyAmount,
                    BilledAmount = billedAmount,
                    PendingAmount = pendingAmount,
                    DelayDays = delayDays,
                    WarehouseId = item.WarehouseId,
                    WarehouseName = warehouseName,
                    CompanyId = order.CompanyId,
                    CompanyName = companyName,
                });
            }
        }

        List<PurchaseOrderAnalysisRowDto> resultRows;

        if (input.GroupByPo)
        {
            resultRows = detailedRows
                .GroupBy(r => r.PurchaseOrderId)
                .Select(g =>
                {
                    var first = g.First();
                    return new PurchaseOrderAnalysisRowDto
                    {
                        PurchaseOrderNumber = first.PurchaseOrderNumber,
                        PurchaseOrderId = first.PurchaseOrderId,
                        Date = first.Date,
                        RequiredDate = g.Min(r => r.RequiredDate),
                        Status = first.Status,
                        SupplierId = first.SupplierId,
                        SupplierName = first.SupplierName,
                        Qty = g.Sum(r => r.Qty),
                        ReceivedQty = g.Sum(r => r.ReceivedQty),
                        PendingQty = g.Sum(r => r.PendingQty),
                        BilledQty = g.Sum(r => r.BilledQty),
                        QtyToBill = g.Sum(r => r.QtyToBill),
                        Amount = g.Sum(r => r.Amount),
                        ReceivedQtyAmount = g.Sum(r => r.ReceivedQtyAmount),
                        BilledAmount = g.Sum(r => r.BilledAmount),
                        PendingAmount = g.Sum(r => r.PendingAmount),
                        DelayDays = g.Max(r => r.DelayDays),
                        CompanyId = first.CompanyId,
                        CompanyName = first.CompanyName,
                    };
                })
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.PurchaseOrderNumber)
                .ToList();
        }
        else if (input.GroupByItem)
        {
            // Group by (company, item_code, uom)
            resultRows = detailedRows
                .GroupBy(r => (r.CompanyId, r.ItemCode, r.Uom))
                .Select(g =>
                {
                    var first = g.First();
                    return new PurchaseOrderAnalysisRowDto
                    {
                        CompanyId = first.CompanyId,
                        CompanyName = first.CompanyName,
                        ItemId = first.ItemId,
                        ItemCode = first.ItemCode,
                        Description = first.Description,
                        Uom = first.Uom,
                        Qty = g.Sum(r => r.Qty),
                        ReceivedQty = g.Sum(r => r.ReceivedQty),
                        PendingQty = g.Sum(r => r.PendingQty),
                        BilledQty = g.Sum(r => r.BilledQty),
                        QtyToBill = g.Sum(r => r.QtyToBill),
                        Amount = g.Sum(r => r.Amount),
                        ReceivedQtyAmount = g.Sum(r => r.ReceivedQtyAmount),
                        BilledAmount = g.Sum(r => r.BilledAmount),
                        PendingAmount = g.Sum(r => r.PendingAmount),
                    };
                })
                .OrderBy(r => r.CompanyName)
                .ThenBy(r => r.ItemCode)
                .ThenBy(r => r.Uom)
                .ToList();
        }
        else
        {
            resultRows = detailedRows
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.PurchaseOrderNumber)
                .ToList();
        }

        return new PurchaseOrderAnalysisReportDto
        {
            Rows = resultRows,
            TotalAmount = detailedRows.Sum(r => r.Amount),
            TotalBilledAmount = detailedRows.Sum(r => r.BilledAmount),
            TotalAmountToBill = detailedRows.Sum(r => r.PendingAmount),
            TotalQty = detailedRows.Sum(r => r.Qty),
            TotalReceivedQty = detailedRows.Sum(r => r.ReceivedQty),
        };
    }
}
