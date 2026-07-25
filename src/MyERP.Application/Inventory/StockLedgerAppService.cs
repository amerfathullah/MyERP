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
}
