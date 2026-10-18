using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Stock Ledger Invariant Check and Variance application service.
/// Validates mathematical consistency of running stock balance, balance value, and FIFO/LIFO queues.
/// Implements ERPNext stock/report/stock_ledger_invariant_check and stock_ledger_variance.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockLedgerInvariantCheckAppService : ApplicationService, IStockLedgerInvariantCheckAppService
{
    private const decimal QtyTolerance = 0.001m;
    private const decimal ValueTolerance = 0.01m;

    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;

    public StockLedgerInvariantCheckAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Batch, Guid> batchRepository)
    {
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
    }

    public async Task<StockLedgerInvariantCheckReportDto> GetInvariantCheckAsync(StockLedgerInvariantCheckFilterDto input)
    {
        var item = await _itemRepository.GetAsync(input.ItemId);
        var warehouse = await _warehouseRepository.GetAsync(input.WarehouseId);

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var sles = sleQuery
            .Where(s => s.ItemId == input.ItemId && s.WarehouseId == input.WarehouseId && !s.IsCancelled)
            .OrderBy(s => s.PostingDateTime)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var batchIds = sles.Where(s => s.BatchId.HasValue).Select(s => s.BatchId!.Value).Distinct().ToList();
        var batches = batchIds.Count > 0
            ? (await _batchRepository.GetListAsync(b => batchIds.Contains(b.Id))).ToDictionary(b => b.Id)
            : new Dictionary<Guid, Batch>();

        decimal balanceQty = 0m;
        decimal balanceStockValue = 0m;
        int? firstViolationIndex = null;
        var rows = new List<StockLedgerInvariantRowDto>(sles.Count);
        decimal? prevFifoStockValue = null;

        var isFifoMethod = item.ValuationMethod == ValuationMethod.FIFO || item.ValuationMethod == ValuationMethod.LIFO;
        var isLifo = item.ValuationMethod == ValuationMethod.LIFO;

        for (int idx = 0; idx < sles.Count; idx++)
        {
            var sle = sles[idx];
            batches.TryGetValue(sle.BatchId ?? Guid.Empty, out var batch);
            var useBatchwiseValuation = batch?.UseBatchwiseValuation ?? false;

            decimal consumptionRate = 0m;
            if (sle.QuantityChange < 0 && sle.QuantityChange != 0)
            {
                consumptionRate = sle.StockValueDifference / sle.QuantityChange;
            }

            balanceQty += sle.QuantityChange;
            balanceStockValue += sle.StockValueDifference;

            var expectedQty = balanceQty;
            var diffInQty = sle.BalanceQuantity - expectedQty;
            var stockValFromDiff = balanceStockValue;
            var diffValDiff = stockValFromDiff - sle.BalanceValue;
            decimal? balanceValueByQty = sle.BalanceQuantity != 0 ? sle.BalanceValue / sle.BalanceQuantity : null;
            decimal? valuationDiff = balanceValueByQty.HasValue ? sle.ValuationRate - balanceValueByQty.Value : null;

            bool maintainsFifo = isFifoMethod && !sle.HasSerialNo && sle.SerialAndBatchBundleId == null && !(sle.HasBatchNo && useBatchwiseValuation);

            decimal? fifoQueueQty = null;
            decimal? fifoStockValue = null;
            decimal? fifoValuationRate = null;
            decimal? fifoQtyDiff = null;
            decimal? fifoValueDiff = null;
            decimal? fifoValuationDiff = null;
            decimal? fifoStockDiff = null;
            decimal? fifoDifferenceDiff = null;

            if (maintainsFifo)
            {
                var queue = FifoValuation.Deserialize(sle.StockQueue, isLifo);
                fifoQueueQty = queue.TotalQty;
                fifoStockValue = queue.TotalValue;
                fifoValuationRate = fifoQueueQty > 0 ? fifoStockValue / fifoQueueQty : null;
                fifoQtyDiff = sle.BalanceQuantity - fifoQueueQty;
                fifoValueDiff = sle.BalanceValue - fifoStockValue;
                fifoValuationDiff = fifoValuationRate.HasValue ? sle.ValuationRate - fifoValuationRate.Value : null;

                if (prevFifoStockValue.HasValue)
                {
                    fifoStockDiff = fifoStockValue.Value - prevFifoStockValue.Value;
                    fifoDifferenceDiff = fifoStockDiff.Value - sle.StockValueDifference;
                }
                prevFifoStockValue = fifoStockValue;
            }
            else
            {
                prevFifoStockValue = null;
            }

            var isViolated = Math.Abs(diffInQty) > QtyTolerance
                || Math.Abs(diffValDiff) > ValueTolerance
                || (maintainsFifo && (Math.Abs(fifoQtyDiff ?? 0m) > QtyTolerance || Math.Abs(fifoValueDiff ?? 0m) > ValueTolerance));

            if (isViolated && firstViolationIndex == null)
            {
                firstViolationIndex = idx;
            }

            var row = new StockLedgerInvariantRowDto
            {
                SleId = sle.Id,
                PostingDateTime = sle.PostingDateTime,
                CreationTime = sle.CreationTime,
                VoucherType = sle.VoucherType,
                VoucherId = sle.VoucherId,
                VoucherNo = sle.VoucherId.HasValue ? $"{sle.VoucherType}-{sle.VoucherId.Value.ToString()[..8].ToUpperInvariant()}" : sle.VoucherType,
                BatchId = sle.BatchId,
                BatchNo = batch?.BatchNo,
                SerialAndBatchBundleId = sle.SerialAndBatchBundleId,
                UseBatchwiseValuation = useBatchwiseValuation,
                QuantityChange = sle.QuantityChange,
                IncomingRate = sle.IncomingRate,
                ConsumptionRate = consumptionRate,
                QtyAfterTransaction = sle.BalanceQuantity,
                ExpectedQtyAfterTransaction = expectedQty,
                DifferenceInQty = diffInQty,
                StockQueue = sle.StockQueue,
                FifoQueueQty = fifoQueueQty,
                FifoQtyDiff = fifoQtyDiff,
                StockValue = sle.BalanceValue,
                FifoStockValue = fifoStockValue,
                FifoValueDiff = fifoValueDiff,
                StockValueDifference = sle.StockValueDifference,
                StockValueFromDiff = stockValFromDiff,
                DiffValueDiff = diffValDiff,
                FifoStockDiff = fifoStockDiff,
                FifoDifferenceDiff = fifoDifferenceDiff,
                ValuationRate = sle.ValuationRate,
                FifoValuationRate = fifoValuationRate,
                FifoValuationDiff = fifoValuationDiff,
                BalanceValueByQty = balanceValueByQty,
                ValuationDiff = valuationDiff,
                IsInvariantViolated = isViolated
            };

            rows.Add(row);
        }

        var totalEntries = rows.Count;
        var violationCount = rows.Count(r => r.IsInvariantViolated);

        if (input.ShowIncorrectEntries)
        {
            if (firstViolationIndex.HasValue)
            {
                var startIdx = Math.Max(0, firstViolationIndex.Value - 1);
                rows = rows.Skip(startIdx).ToList();
            }
            else
            {
                rows = new List<StockLedgerInvariantRowDto>();
            }
        }

        return new StockLedgerInvariantCheckReportDto
        {
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            StockUom = item.Uom,
            ValuationMethod = item.ValuationMethod.ToString(),
            WarehouseId = warehouse.Id,
            WarehouseName = warehouse.Name,
            CompanyId = warehouse.CompanyId,
            TotalEntries = totalEntries,
            ViolationCount = violationCount,
            FirstViolationIndex = firstViolationIndex,
            Rows = rows
        };
    }

    public async Task<StockLedgerVarianceReportDto> GetStockLedgerVarianceAsync(StockLedgerVarianceFilterDto input)
    {
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        whQuery = whQuery.Where(w => !w.IsGroup && w.IsActive);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.Id == input.WarehouseId.Value);
        }
        var warehouses = whQuery.ToList();
        var whMap = warehouses.ToDictionary(w => w.Id);
        var allowedWhIds = whMap.Keys.ToHashSet();

        if (allowedWhIds.Count == 0)
        {
            return new StockLedgerVarianceReportDto();
        }

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var baseQuery = sleQuery.Where(s => !s.IsCancelled && allowedWhIds.Contains(s.WarehouseId));
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(s => s.ItemId == input.ItemId.Value);
        }

        var sles = baseQuery
            .OrderBy(s => s.PostingDateTime)
            .ThenBy(s => s.CreationTime)
            .ToList();

        if (sles.Count == 0)
        {
            return new StockLedgerVarianceReportDto();
        }

        var itemIds = sles.Select(s => s.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id);

        var groups = sles.GroupBy(s => (s.ItemId, s.WarehouseId));
        var varianceRows = new List<StockLedgerVarianceRowDto>();

        foreach (var group in groups)
        {
            if (!itemMap.TryGetValue(group.Key.ItemId, out var item))
                continue;
            if (!whMap.TryGetValue(group.Key.WarehouseId, out var wh))
                continue;

            decimal balanceQty = 0m;
            decimal balanceStockValue = 0m;

            foreach (var sle in group)
            {
                balanceQty += sle.QuantityChange;
                balanceStockValue += sle.StockValueDifference;

                var expectedQty = balanceQty;
                var diffInQty = sle.BalanceQuantity - expectedQty;
                var stockValFromDiff = balanceStockValue;
                var diffValDiff = stockValFromDiff - sle.BalanceValue;
                decimal? balanceValueByQty = sle.BalanceQuantity != 0 ? sle.BalanceValue / sle.BalanceQuantity : null;
                decimal? valuationDiff = balanceValueByQty.HasValue ? sle.ValuationRate - balanceValueByQty.Value : null;

                var hasViolation = Math.Abs(diffInQty) > QtyTolerance || Math.Abs(diffValDiff) > ValueTolerance;

                if (!input.ShowOnlyViolations || hasViolation)
                {
                    varianceRows.Add(new StockLedgerVarianceRowDto
                    {
                        SleId = sle.Id,
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        WarehouseId = wh.Id,
                        WarehouseName = wh.Name,
                        ValuationMethod = item.ValuationMethod.ToString(),
                        PostingDateTime = sle.PostingDateTime,
                        CreationTime = sle.CreationTime,
                        VoucherType = sle.VoucherType,
                        VoucherId = sle.VoucherId,
                        VoucherNo = sle.VoucherId.HasValue ? $"{sle.VoucherType}-{sle.VoucherId.Value.ToString()[..8].ToUpperInvariant()}" : sle.VoucherType,
                        QuantityChange = sle.QuantityChange,
                        QtyAfterTransaction = sle.BalanceQuantity,
                        ExpectedQtyAfterTransaction = expectedQty,
                        DifferenceInQty = diffInQty,
                        StockValue = sle.BalanceValue,
                        StockValueFromDiff = stockValFromDiff,
                        DiffValueDiff = diffValDiff,
                        ValuationRate = sle.ValuationRate,
                        BalanceValueByQty = balanceValueByQty,
                        ValuationDiff = valuationDiff,
                        HasViolation = hasViolation
                    });
                }
            }
        }

        return new StockLedgerVarianceReportDto
        {
            TotalViolations = varianceRows.Count(r => r.HasViolation),
            Rows = varianceRows
        };
    }
}
