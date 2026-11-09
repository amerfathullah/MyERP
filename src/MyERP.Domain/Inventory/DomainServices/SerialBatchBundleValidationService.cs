using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Validates Serial and Batch Bundle (SABB) company isolation on stock movements.
/// Per ERPNext PR #58608: validates that any bundle referenced by a transaction line
/// belongs to the same Company as the parent document.
/// </summary>
public class SerialBatchBundleValidationService : DomainService
{
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;

    public SerialBatchBundleValidationService(IRepository<SerialAndBatchBundle, Guid> bundleRepository)
    {
        _bundleRepository = bundleRepository;
    }

    /// <summary>
    /// Validates that all bundles referenced in the transaction match the expected company.
    /// </summary>
    public async Task ValidateBundleCompanyAsync(
        Guid expectedCompanyId,
        IEnumerable<Guid?> bundleIds,
        bool isInternalTransfer = false)
    {
        var validBundleIds = bundleIds
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (!validBundleIds.Any())
            return;

        var bundles = await _bundleRepository.GetListAsync(b => validBundleIds.Contains(b.Id));
        var bundleMap = bundles.ToDictionary(b => b.Id);

        foreach (var bundleId in validBundleIds)
        {
            if (bundleMap.TryGetValue(bundleId, out var bundle))
            {
                if (bundle.IsCancelled)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                        .WithData("bundleId", bundle.Id)
                        .WithData("reason", $"Serial and Batch Bundle {bundle.Id} is cancelled and cannot be used in a stock transaction");
                }

                if (bundle.CompanyId != expectedCompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyRestrictionBlocked)
                        .WithData("bundleId", bundle.Id)
                        .WithData("bundleCompanyId", bundle.CompanyId)
                        .WithData("expectedCompanyId", expectedCompanyId)
                        .WithData("reason", $"Serial and Batch Bundle {bundle.Id} belongs to company {bundle.CompanyId}, not {expectedCompanyId}");
                }
            }
        }
    }

    /// <summary>
    /// Filters batches from a source document row for a return transaction.
    /// Per ERPNext commits dd07d12215 & c9257731eb (filter_serial_batches):
    /// When a purchase/subcontracting return row had multiple batches on the source document and one or more
    /// of those batches are no longer in the warehouse, skip unavailable batches if the remaining batches
    /// cover the return quantity, and throw only when NO batch is available in the warehouse.
    /// </summary>
    public static Dictionary<Guid, decimal> FilterReturnBatches(
        IReadOnlyDictionary<Guid, decimal> sourceBatches,
        decimal requiredReturnQty,
        Guid warehouseId,
        IReadOnlyDictionary<Guid, decimal> availableBatchQuantitiesInWarehouse,
        bool isPurchaseReturn = true,
        bool isInternalCustomer = false)
    {
        var filteredBatches = new Dictionary<Guid, decimal>();
        var qty = Math.Abs(requiredReturnQty);
        if (qty <= 0 || sourceBatches == null || sourceBatches.Count == 0)
        {
            return filteredBatches;
        }

        Guid? unavailableBatchId = null;

        foreach (var (batchId, rawBatchQty) in sourceBatches)
        {
            if (qty <= 0)
            {
                break;
            }

            var batchQty = isInternalCustomer ? -rawBatchQty : Math.Abs(rawBatchQty);

            if (isPurchaseReturn)
            {
                var availableInWh = availableBatchQuantitiesInWarehouse.GetValueOrDefault(batchId, 0m);
                batchQty = Math.Min(batchQty, availableInWh);

                if (batchQty <= 0)
                {
                    unavailableBatchId ??= batchId;
                    continue;
                }
            }

            if (batchQty > qty)
            {
                filteredBatches[batchId] = qty;
                qty = 0m;
            }
            else
            {
                filteredBatches[batchId] = filteredBatches.GetValueOrDefault(batchId, 0m) + batchQty;
                qty -= batchQty;
            }
        }

        if (unavailableBatchId.HasValue && filteredBatches.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("batchId", unavailableBatchId.Value)
                .WithData("warehouseId", warehouseId)
                .WithData("detail", $"Batch {unavailableBatchId.Value} is not available in warehouse {warehouseId}.");
        }

        return filteredBatches;
    }

    /// <summary>
    /// Resolves current warehouse batch balances from the Stock Ledger and filters source return batches.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> FilterReturnBatchesAsync(
        IReadOnlyDictionary<Guid, decimal> sourceBatches,
        decimal requiredReturnQty,
        Guid warehouseId,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        bool isPurchaseReturn = true,
        bool isInternalCustomer = false)
    {
        if (sourceBatches == null || sourceBatches.Count == 0 || Math.Abs(requiredReturnQty) <= 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var batchIds = sourceBatches.Keys.ToList();
        var sleQuery = await sleRepository.GetQueryableAsync();
        var availableMap = sleQuery
            .Where(s => !s.IsCancelled
                     && s.WarehouseId == warehouseId
                     && s.BatchId.HasValue
                     && batchIds.Contains(s.BatchId.Value))
            .GroupBy(s => s.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Qty = g.Sum(s => s.QuantityChange) })
            .ToDictionary(x => x.BatchId, x => x.Qty);

        return FilterReturnBatches(
            sourceBatches,
            requiredReturnQty,
            warehouseId,
            availableMap,
            isPurchaseReturn,
            isInternalCustomer);
    }
}
