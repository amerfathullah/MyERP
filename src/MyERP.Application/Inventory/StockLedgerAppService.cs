using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class StockLedgerAppService : ApplicationService, IStockLedgerAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _ledgerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public StockLedgerAppService(
        IRepository<StockLedgerEntry, Guid> ledgerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _ledgerRepository = ledgerRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<StockLedgerReportDto> GetStockLedgerAsync(StockLedgerRequestDto input)
    {
        var query = await _ledgerRepository.GetQueryableAsync();

        query = query.Where(e => e.CompanyId == input.CompanyId
            && e.PostingDate >= input.FromDate
            && e.PostingDate <= input.ToDate);

        if (input.ItemId.HasValue)
            query = query.Where(e => e.ItemId == input.ItemId.Value);

        if (input.WarehouseId.HasValue)
            query = query.Where(e => e.WarehouseId == input.WarehouseId.Value);

        var entries = query.OrderBy(e => e.PostingDate).ThenBy(e => e.CreationTime).ToList();

        // Build lookup dictionaries
        var itemIds = entries.Select(e => e.ItemId).Distinct().ToList();
        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToList();

        var items = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id, i => i.ItemName);
        var warehouses = (await _warehouseRepository.GetListAsync(w => warehouseIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.Name);

        var rows = entries.Select(e => new StockLedgerRowDto
        {
            PostingDate = e.PostingDate,
            ItemName = items.GetValueOrDefault(e.ItemId, "Unknown"),
            WarehouseName = warehouses.GetValueOrDefault(e.WarehouseId, "Unknown"),
            QuantityChange = e.QuantityChange,
            ValuationRate = e.ValuationRate,
            StockValue = e.StockValue,
            BalanceQuantity = e.BalanceQuantity,
            BalanceValue = e.BalanceValue,
            VoucherType = e.VoucherType,
            VoucherId = e.VoucherId,
        }).ToList();

        return new StockLedgerReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            TotalIn = rows.Where(r => r.QuantityChange > 0).Sum(r => r.QuantityChange),
            TotalOut = Math.Abs(rows.Where(r => r.QuantityChange < 0).Sum(r => r.QuantityChange)),
        };
    }

    /// <summary>
    /// Returns all SLE entries posted by a specific source document (per ERPNext "Stock Ledger" button on document detail pages).
    /// Used on DN/PR/SE/SI(UpdateStock)/PI(UpdateStock)/WO detail pages.
    /// </summary>
    public async Task<VoucherStockLedgerDto> GetForVoucherAsync(string voucherType, Guid voucherId)
    {
        var query = await _ledgerRepository.GetQueryableAsync();
        var entries = query
            .Where(e => e.VoucherType == voucherType && e.VoucherId == voucherId)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.CreationTime)
            .ToList();

        if (!entries.Any())
            return new VoucherStockLedgerDto { VoucherType = voucherType, VoucherId = voucherId };

        // Resolve item + warehouse names
        var itemIds = entries.Select(e => e.ItemId).Distinct().ToList();
        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToList();

        var items = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });
        var warehouses = (await _warehouseRepository.GetListAsync(w => warehouseIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.Name);

        var rows = entries.Select(e => new VoucherStockLedgerEntryDto
        {
            PostingDate = e.PostingDate,
            ItemCode = items.GetValueOrDefault(e.ItemId)?.ItemCode,
            ItemName = items.GetValueOrDefault(e.ItemId)?.ItemName,
            WarehouseName = warehouses.GetValueOrDefault(e.WarehouseId, "Unknown"),
            QuantityChange = e.QuantityChange,
            ValuationRate = e.ValuationRate,
            StockValueDifference = e.StockValue,
            BalanceQuantity = e.BalanceQuantity,
            BalanceValue = e.BalanceValue,
        }).ToList();

        return new VoucherStockLedgerDto
        {
            VoucherType = voucherType,
            VoucherId = voucherId,
            Entries = rows,
            TotalQtyIn = rows.Where(r => r.QuantityChange > 0).Sum(r => r.QuantityChange),
            TotalQtyOut = Math.Abs(rows.Where(r => r.QuantityChange < 0).Sum(r => r.QuantityChange)),
            TotalValueDifference = rows.Sum(r => r.StockValueDifference),
        };
    }

    /// <summary>
    /// Summarizes stock movements per item for a period: opening, in, out, closing qty + value.
    /// Per ERPNext Stock Ledger / Stock Balance Analysis report pattern.
    /// </summary>
    public async Task<StockMovementSummaryDto> GetStockMovementSummaryAsync(
        Guid companyId, DateTime fromDate, DateTime toDate, Guid? warehouseId = null)
    {
        var query = await _ledgerRepository.GetQueryableAsync();

        // Opening balance: SLEs before period start
        var openingQuery = query.Where(e => e.CompanyId == companyId && e.PostingDate < fromDate);
        if (warehouseId.HasValue)
            openingQuery = openingQuery.Where(e => e.WarehouseId == warehouseId.Value);

        var openingByItem = openingQuery
            .GroupBy(e => e.ItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(e => e.QuantityChange), Value = g.Sum(e => e.StockValue) })
            .ToList()
            .ToDictionary(x => x.ItemId);

        // Period movements: SLEs within period
        var periodQuery = query.Where(e => e.CompanyId == companyId && e.PostingDate >= fromDate && e.PostingDate <= toDate);
        if (warehouseId.HasValue)
            periodQuery = periodQuery.Where(e => e.WarehouseId == warehouseId.Value);

        var movementsByItem = periodQuery
            .GroupBy(e => e.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                StockIn = g.Where(e => e.QuantityChange > 0).Sum(e => e.QuantityChange),
                StockOut = g.Where(e => e.QuantityChange < 0).Sum(e => e.QuantityChange),
                ValueIn = g.Where(e => e.QuantityChange > 0).Sum(e => e.StockValue),
                ValueOut = g.Where(e => e.QuantityChange < 0).Sum(e => e.StockValue),
            })
            .ToList();

        var allItemIds = openingByItem.Keys.Union(movementsByItem.Select(m => m.ItemId)).Distinct().ToList();
        var itemNames = (await _itemRepository.GetListAsync(i => allItemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        var rows = allItemIds.Select(itemId =>
        {
            var opening = openingByItem.GetValueOrDefault(itemId);
            var movement = movementsByItem.FirstOrDefault(m => m.ItemId == itemId);
            var openingQty = opening?.Qty ?? 0;
            var stockIn = movement?.StockIn ?? 0;
            var stockOut = Math.Abs(movement?.StockOut ?? 0);
            var item = itemNames.GetValueOrDefault(itemId);
            return new StockMovementItemDto
            {
                ItemId = itemId,
                ItemCode = item?.ItemCode ?? "",
                ItemName = item?.ItemName ?? "",
                OpeningQty = openingQty,
                StockInQty = stockIn,
                StockOutQty = stockOut,
                ClosingQty = openingQty + stockIn - stockOut,
                StockInValue = movement?.ValueIn ?? 0,
                StockOutValue = Math.Abs(movement?.ValueOut ?? 0),
            };
        }).Where(r => r.OpeningQty != 0 || r.StockInQty != 0 || r.StockOutQty != 0)
          .OrderByDescending(r => r.StockInQty + r.StockOutQty)
          .ToList();

        return new StockMovementSummaryDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalItems = rows.Count,
            TotalStockIn = rows.Sum(r => r.StockInQty),
            TotalStockOut = rows.Sum(r => r.StockOutQty),
            TotalStockInValue = rows.Sum(r => r.StockInValue),
            TotalStockOutValue = rows.Sum(r => r.StockOutValue),
            Items = rows,
        };
    }
}
