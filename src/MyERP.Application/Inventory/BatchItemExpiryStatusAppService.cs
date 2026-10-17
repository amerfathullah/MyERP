using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Batch Item Expiry Status application service.
/// Identifies batches created in a date range with remaining positive stock,
/// tracking expiration days, expired batches, and batches expiring soon.
/// Maps to ERPNext stock/report/batch_item_expiry_status/batch_item_expiry_status.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class BatchItemExpiryStatusAppService : ApplicationService, IBatchItemExpiryStatusAppService
{
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public BatchItemExpiryStatusAppService(
        IRepository<Batch, Guid> batchRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _sleRepository = sleRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<BatchItemExpiryStatusReportDto> GetReportAsync(BatchItemExpiryStatusRequestDto input)
    {
        var fromDate = input.FromDate.Date;
        var toDateCutoff = input.ToDate.Date.AddDays(1).AddTicks(-1);

        var batchQuery = await _batchRepository.GetQueryableAsync();
        var batches = batchQuery
            .Where(b => !b.IsDisabled && !b.IsCancelled && b.CreationTime >= fromDate && b.CreationTime <= toDateCutoff)
            .ToList();

        if (input.ItemId.HasValue)
        {
            batches = batches.Where(b => b.ItemId == input.ItemId.Value).ToList();
        }

        if (batches.Count == 0)
        {
            return new BatchItemExpiryStatusReportDto
            {
                FromDate = input.FromDate,
                ToDate = input.ToDate
            };
        }

        var batchIds = batches.Select(b => b.Id).ToHashSet();
        var batchMap = batches.ToDictionary(b => b.Id);

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var sles = sleQuery
            .Where(s => !s.IsCancelled && s.BatchId != null && batchIds.Contains(s.BatchId.Value))
            .ToList();

        if (input.CompanyId.HasValue)
        {
            sles = sles.Where(s => s.CompanyId == input.CompanyId.Value).ToList();
        }
        if (input.WarehouseId.HasValue)
        {
            sles = sles.Where(s => s.WarehouseId == input.WarehouseId.Value).ToList();
        }

        var batchBalances = sles
            .GroupBy(s => new { BatchId = s.BatchId!.Value, s.WarehouseId, s.ItemId })
            .Select(g => new
            {
                g.Key.BatchId,
                g.Key.WarehouseId,
                g.Key.ItemId,
                Quantity = g.Sum(s => s.QuantityChange)
            })
            .Where(g => g.Quantity > 0)
            .ToList();

        var itemIds = batches.Select(b => b.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemDict = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Uom })
            .ToList()
            .ToDictionary(i => i.Id);

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var whDict = whQuery.Select(w => new { w.Id, w.Name }).ToList().ToDictionary(w => w.Id, w => w.Name);

        var today = DateTime.UtcNow.Date;
        var rows = new List<BatchItemExpiryStatusRowDto>();

        foreach (var balance in batchBalances)
        {
            var batch = batchMap[balance.BatchId];
            var item = itemDict.TryGetValue(balance.ItemId, out var itm) ? itm : null;
            var whName = whDict.TryGetValue(balance.WarehouseId, out var wn) ? wn : null;

            int? expiryInDays = null;
            bool isExpired = false;
            bool isExpiringSoon = false;

            if (batch.ExpiryDate.HasValue)
            {
                var diff = (int)(batch.ExpiryDate.Value.Date - today).TotalDays;
                expiryInDays = diff;
                isExpired = diff < 0;
                isExpiringSoon = diff >= 0 && diff <= 30;
            }

            rows.Add(new BatchItemExpiryStatusRowDto
            {
                BatchId = batch.Id,
                BatchNo = batch.BatchNo,
                ItemId = balance.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                StockUom = item?.Uom ?? "Unit",
                Quantity = balance.Quantity,
                ManufacturingDate = batch.ManufacturingDate,
                ExpiryDate = batch.ExpiryDate,
                ExpiryInDays = expiryInDays,
                IsExpired = isExpired,
                IsExpiringSoon = isExpiringSoon,
                WarehouseId = balance.WarehouseId,
                WarehouseName = whName
            });
        }

        rows = rows.OrderBy(r => r.ExpiryInDays ?? int.MaxValue).ToList();

        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            var search = input.SearchText.Trim();
            rows = rows.Where(r =>
                r.BatchNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(r.WarehouseName) && r.WarehouseName.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        return new BatchItemExpiryStatusReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            TotalBatches = rows.Select(r => r.BatchId).Distinct().Count(),
            ExpiredCount = rows.Where(r => r.IsExpired).Select(r => r.BatchId).Distinct().Count(),
            ExpiringSoonCount = rows.Where(r => r.IsExpiringSoon).Select(r => r.BatchId).Distinct().Count(),
            TotalQuantity = rows.Sum(r => r.Quantity)
        };
    }
}
