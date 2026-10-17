using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Item-wise Consumption application service.
/// Tracks outward stock movements (consumed in production/issues vs delivered in sales),
/// excluding cancelled, adjustments, and inter-warehouse material transfers.
/// Maps to ERPNext stock/report/item_wise_consumption/item_wise_consumption.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class ItemWiseConsumptionAppService : ApplicationService, IItemWiseConsumptionAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;

    public ItemWiseConsumptionAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Supplier, Guid> supplierRepository)
    {
        _sleRepository = sleRepository;
        _stockEntryRepository = stockEntryRepository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<ItemWiseConsumptionReportDto> GetReportAsync(ItemWiseConsumptionRequestDto input)
    {
        var fromDate = input.FromDate.Date;
        var toDateCutoff = input.ToDate.Date.AddDays(1).AddTicks(-1);

        // 1. Identify material transfer stock entry IDs to exclude from consumption
        var seQuery = await _stockEntryRepository.GetQueryableAsync();
        var transferVoucherIds = seQuery
            .Where(se => se.EntryType == StockEntryType.MaterialTransfer ||
                         se.EntryType == StockEntryType.SendToWarehouse ||
                         se.EntryType == StockEntryType.ReceiveAtWarehouse)
            .Select(se => se.Id)
            .ToHashSet();

        // 2. Fetch outward SLEs
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var outwardSles = sleQuery
            .Where(s => !s.IsCancelled &&
                         !s.IsAdjustmentEntry &&
                         s.QuantityChange < 0 &&
                         s.PostingDateTime >= fromDate &&
                         s.PostingDateTime <= toDateCutoff)
            .ToList();

        if (input.CompanyId.HasValue)
        {
            outwardSles = outwardSles.Where(s => s.CompanyId == input.CompanyId.Value).ToList();
        }

        if (input.WarehouseId.HasValue)
        {
            outwardSles = outwardSles.Where(s => s.WarehouseId == input.WarehouseId.Value).ToList();
        }

        if (input.ItemId.HasValue)
        {
            outwardSles = outwardSles.Where(s => s.ItemId == input.ItemId.Value).ToList();
        }

        // Exclude material transfer vouchers
        outwardSles = outwardSles
            .Where(s => !(s.VoucherId.HasValue && transferVoucherIds.Contains(s.VoucherId.Value)))
            .ToList();

        if (outwardSles.Count == 0)
        {
            return new ItemWiseConsumptionReportDto
            {
                FromDate = input.FromDate,
                ToDate = input.ToDate
            };
        }

        // 3. Aggregate by Item
        var itemGroups = outwardSles.GroupBy(s => s.ItemId).ToList();
        var itemIds = itemGroups.Select(g => g.Key).ToList();

        // 4. Fetch Items with Suppliers
        var itemQuery = await _itemRepository.WithDetailsAsync(i => i.Suppliers);
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToList();
        var itemDict = items.ToDictionary(i => i.Id);

        // Fetch supplier names
        var supplierIds = items.SelectMany(i => i.Suppliers.Select(s => s.SupplierId)).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var supplierDict = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToList()
            .ToDictionary(s => s.Id, s => s.Name);

        var rows = new List<ItemWiseConsumptionRowDto>();

        foreach (var group in itemGroups)
        {
            if (!itemDict.TryGetValue(group.Key, out var item))
            {
                continue;
            }

            // Check supplier filter if provided
            if (input.SupplierId.HasValue && !item.Suppliers.Any(s => s.SupplierId == input.SupplierId.Value))
            {
                continue;
            }

            decimal consumedQty = 0m;
            decimal consumedAmount = 0m;
            decimal deliveredQty = 0m;
            decimal deliveredAmount = 0m;

            foreach (var sle in group)
            {
                var isDelivered = sle.VoucherType != null &&
                                  (sle.VoucherType.Equals("DeliveryNote", StringComparison.OrdinalIgnoreCase) ||
                                   sle.VoucherType.Equals("Delivery Note", StringComparison.OrdinalIgnoreCase) ||
                                   sle.VoucherType.Equals("SalesInvoice", StringComparison.OrdinalIgnoreCase) ||
                                   sle.VoucherType.Equals("Sales Invoice", StringComparison.OrdinalIgnoreCase));

                if (isDelivered)
                {
                    deliveredQty += Math.Abs(sle.QuantityChange);
                    deliveredAmount += Math.Abs(sle.StockValueDifference != 0 ? sle.StockValueDifference : sle.QuantityChange * sle.ValuationRate);
                }
                else
                {
                    consumedQty += Math.Abs(sle.QuantityChange);
                    consumedAmount += Math.Abs(sle.StockValueDifference != 0 ? sle.StockValueDifference : sle.QuantityChange * sle.ValuationRate);
                }
            }

            var supplierNames = item.Suppliers
                .Select(s => supplierDict.TryGetValue(s.SupplierId, out var name) ? name : null)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            var suppliersText = supplierNames.Count > 0 ? string.Join(", ", supplierNames) : null;

            rows.Add(new ItemWiseConsumptionRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                StockUom = item.Uom ?? "Unit",
                ConsumedQty = consumedQty,
                ConsumedAmount = consumedAmount,
                DeliveredQty = deliveredQty,
                DeliveredAmount = deliveredAmount,
                TotalQty = consumedQty + deliveredQty,
                TotalAmount = consumedAmount + deliveredAmount,
                Suppliers = suppliersText
            });
        }

        // Search text filtering
        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            var search = input.SearchText.Trim();
            rows = rows.Where(r =>
                r.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(r.Description) && r.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.Suppliers) && r.Suppliers.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Order by TotalAmount descending
        rows = rows.OrderByDescending(r => r.TotalAmount).ToList();

        return new ItemWiseConsumptionReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            TotalItems = rows.Count,
            GrandTotalConsumedQty = rows.Sum(r => r.ConsumedQty),
            GrandTotalConsumedAmount = rows.Sum(r => r.ConsumedAmount),
            GrandTotalDeliveredQty = rows.Sum(r => r.DeliveredQty),
            GrandTotalDeliveredAmount = rows.Sum(r => r.DeliveredAmount),
            GrandTotalQty = rows.Sum(r => r.TotalQty),
            GrandTotalAmount = rows.Sum(r => r.TotalAmount)
        };
    }
}
