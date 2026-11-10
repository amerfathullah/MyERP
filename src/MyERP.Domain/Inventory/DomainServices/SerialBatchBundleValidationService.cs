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
    /// Filters batches by batch number from a source document row for a return transaction.
    /// Per ERPNext PR #64590 (commits c919eeb108 & c4b462c136):
    /// Capped by available batch quantity in warehouse; skips unavailable batches if other batches cover the return.
    /// </summary>
    public static Dictionary<string, decimal> FilterReturnBatches(
        IReadOnlyDictionary<string, decimal> sourceBatches,
        decimal requiredReturnQty,
        Guid warehouseId,
        IReadOnlyDictionary<string, decimal> availableBatchQuantitiesInWarehouse,
        bool isPurchaseReturn = true,
        bool isInternalCustomer = false)
    {
        var filteredBatches = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var qty = Math.Abs(requiredReturnQty);
        if (qty <= 0 || sourceBatches == null || sourceBatches.Count == 0)
        {
            return filteredBatches;
        }

        string? unavailableBatchNo = null;

        foreach (var (batchNo, rawBatchQty) in sourceBatches)
        {
            if (qty <= 0)
            {
                break;
            }

            var batchQty = isInternalCustomer ? -rawBatchQty : Math.Abs(rawBatchQty);

            if (isPurchaseReturn)
            {
                var availableInWh = availableBatchQuantitiesInWarehouse.GetValueOrDefault(batchNo, 0m);
                batchQty = Math.Min(batchQty, availableInWh);

                if (batchQty <= 0)
                {
                    unavailableBatchNo ??= batchNo;
                    continue;
                }
            }

            if (batchQty > qty)
            {
                filteredBatches[batchNo] = qty;
                qty = 0m;
            }
            else
            {
                filteredBatches[batchNo] = filteredBatches.GetValueOrDefault(batchNo, 0m) + batchQty;
                qty -= batchQty;
            }
        }

        if (unavailableBatchNo != null && filteredBatches.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("batchNo", unavailableBatchNo)
                .WithData("warehouseId", warehouseId)
                .WithData("detail", $"Batch {unavailableBatchNo} is not available in warehouse {warehouseId}.");
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

    /// <summary>
    /// Determines whether a return row needs an accepted-qty Serial and Batch Bundle lookup.
    /// Per ERPNext commits 2f2d383015 and 889b5fa479:
    /// Return rows with zero accepted qty (e.g. returning only rejected qty from a fully-rejected receipt)
    /// or with ReturnQtyFromRejectedWarehouse = true must be excluded from accepted-qty bundle lookup
    /// so they do not fail with "No Serial / Batches are available for return" before the rejected bundle loop.
    /// </summary>
    public static bool NeedsAcceptedReturnBundle(
        decimal acceptedQty,
        bool returnQtyFromRejectedWarehouse,
        Guid? existingAcceptedBundleId = null,
        bool useSerialBatchFields = false)
    {
        if (useSerialBatchFields || returnQtyFromRejectedWarehouse)
        {
            return false;
        }

        return Math.Abs(acceptedQty) > 0m
            && (!existingAcceptedBundleId.HasValue || existingAcceptedBundleId.Value == Guid.Empty);
    }

    /// <summary>
    /// Determines whether a return row needs a rejected-qty Serial and Batch Bundle lookup.
    /// Per ERPNext commit b96fbf92df (needs_rejected_bundle):
    /// - If ReturnQtyFromRejectedWarehouse is true, the rejected return bundle lives in SerialAndBatchBundleId
    ///   (existingAcceptedBundleId), so it only needs one if existingAcceptedBundleId is not set.
    /// - Otherwise, it needs one only when RejectedWarehouseId is set, RejectedQty != 0, and
    ///   RejectedSerialAndBatchBundleId (existingRejectedBundleId) is not yet set.
    /// </summary>
    public static bool NeedsRejectedReturnBundle(
        decimal rejectedQty,
        Guid? rejectedWarehouseId,
        bool returnQtyFromRejectedWarehouse,
        Guid? existingAcceptedBundleId = null,
        Guid? existingRejectedBundleId = null,
        bool useSerialBatchFields = false)
    {
        if (useSerialBatchFields)
        {
            return false;
        }

        if (returnQtyFromRejectedWarehouse)
        {
            return !existingAcceptedBundleId.HasValue || existingAcceptedBundleId.Value == Guid.Empty;
        }

        return rejectedWarehouseId.HasValue
            && rejectedWarehouseId.Value != Guid.Empty
            && Math.Abs(rejectedQty) > 0m
            && (!existingRejectedBundleId.HasValue || existingRejectedBundleId.Value == Guid.Empty);
    }

    /// <summary>
    /// Drops picked serial numbers and deducts picked batch quantities so subsequent return rows against the same
    /// source row do not pick them again.
    /// Per ERPNext PR #64590 (commits 9403e3df8d, c919eeb108, c4b462c136).
    /// </summary>
    public static void RemovePickedSerialBatches(
        ISet<string> availableSerialNos,
        IDictionary<Guid, decimal> availableBatches,
        IEnumerable<string> pickedSerialNos,
        IReadOnlyDictionary<Guid, decimal> pickedBatches,
        bool isInternalCustomer = false,
        int precision = 4)
    {
        foreach (var sn in pickedSerialNos)
        {
            availableSerialNos.Remove(sn);
        }

        var sign = isInternalCustomer ? -1m : 1m;
        foreach (var (batchId, pickedQty) in pickedBatches)
        {
            if (availableBatches.TryGetValue(batchId, out var currentQty))
            {
                var remaining = Math.Round(currentQty - (sign * pickedQty), precision);
                if (remaining <= 0)
                {
                    availableBatches.Remove(batchId);
                }
                else
                {
                    availableBatches[batchId] = remaining;
                }
            }
        }
    }

    /// <summary>
    /// Drops picked serial numbers and deducts picked batch quantities by batch number so subsequent return rows against the same
    /// source row do not pick them again.
    /// Per ERPNext PR #64590 (commits 9403e3df8d, c919eeb108, c4b462c136).
    /// </summary>
    public static void RemovePickedSerialBatches(
        ISet<string> availableSerialNos,
        IDictionary<string, decimal> availableBatches,
        IEnumerable<string> pickedSerialNos,
        IReadOnlyDictionary<string, decimal> pickedBatches,
        bool isInternalCustomer = false,
        int precision = 4)
    {
        foreach (var sn in pickedSerialNos)
        {
            availableSerialNos.Remove(sn);
        }

        var sign = isInternalCustomer ? -1m : 1m;
        foreach (var (batchNo, pickedQty) in pickedBatches)
        {
            if (availableBatches.TryGetValue(batchNo, out var currentQty))
            {
                var remaining = Math.Round(currentQty - (sign * pickedQty), precision);
                if (remaining <= 0)
                {
                    availableBatches.Remove(batchNo);
                }
                else
                {
                    availableBatches[batchNo] = remaining;
                }
            }
        }
    }

    /// <summary>
    /// Sequentially picks serial numbers and batch quantities for a return line from an available pool,
    /// depleting the pool so subsequent return lines against the same original line pick different serials
    /// and the remaining batch quantity.
    /// Per ERPNext PR #64590 (commits 9403e3df8d, c919eeb108, c4b462c136).
    /// </summary>
    public static PickedSerialBatchResult PickSerialBatchesForReturn(
        ReturnSerialBatchPool pool,
        decimal requiredQty,
        Guid warehouseId,
        IReadOnlyDictionary<Guid, decimal>? availableBatchQtyInWh = null,
        IReadOnlyDictionary<string, decimal>? availableBatchQtyByNoInWh = null,
        bool isPurchaseReturn = true,
        bool isInternalCustomer = false,
        int precision = 4)
    {
        var result = new PickedSerialBatchResult();
        var qty = Math.Abs(requiredQty);
        if (qty <= 0) return result;

        // Serialized items: pick distinct serial numbers up to requiredQty
        if (pool.SerialNos.Count > 0)
        {
            var countToPick = (int)Math.Min(qty, pool.SerialNos.Count);
            result.SerialNos.AddRange(pool.SerialNos.Take(countToPick));
        }

        // Batched items by Guid:
        if (pool.Batches.Count > 0)
        {
            var filtered = FilterReturnBatches(
                pool.Batches,
                qty,
                warehouseId,
                availableBatchQtyInWh ?? new Dictionary<Guid, decimal>(),
                isPurchaseReturn,
                isInternalCustomer);

            foreach (var (bId, bQty) in filtered)
            {
                result.Batches[bId] = bQty;
            }
        }

        // Batched items by string batch number:
        if (pool.BatchesByNo.Count > 0)
        {
            var filtered = FilterReturnBatches(
                pool.BatchesByNo,
                qty,
                warehouseId,
                availableBatchQtyByNoInWh ?? new Dictionary<string, decimal>(),
                isPurchaseReturn,
                isInternalCustomer);

            foreach (var (bNo, bQty) in filtered)
            {
                result.BatchesByNo[bNo] = bQty;
            }
        }

        // Deduct picked serials and batches from pool
        RemovePickedSerialBatches(
            pool.SerialNos,
            pool.Batches,
            result.SerialNos,
            result.Batches,
            isInternalCustomer,
            precision);

        RemovePickedSerialBatches(
            pool.SerialNos,
            pool.BatchesByNo,
            result.SerialNos,
            result.BatchesByNo,
            isInternalCustomer,
            precision);

        return result;
    }

    /// <summary>
    /// Updates available batch quantities by adjusting reservation quantities.
    /// Per ERPNext PR #64860 (commits 6caabeb477, 73c5f3a6b4):
    /// A reservation of a batch that is not available (e.g. expired or not present in warehouse) has nothing
    /// to reduce. When addMissing is false, missing batches are skipped and not added to the pool.
    /// </summary>
    public static void UpdateAvailableBatches(
        IDictionary<Guid, decimal> availableBatches,
        IEnumerable<KeyValuePair<Guid, decimal>> batchAdjustments,
        bool addMissing = true,
        int precision = 4)
    {
        foreach (var (batchId, qty) in batchAdjustments)
        {
            if (availableBatches.TryGetValue(batchId, out var currentQty))
            {
                availableBatches[batchId] = Math.Round(currentQty + qty, precision);
            }
            else if (addMissing)
            {
                availableBatches[batchId] = Math.Round(qty, precision);
            }
        }
    }

    /// <summary>
    /// Updates available batch quantities by batch number.
    /// Per ERPNext PR #64860 (commits 6caabeb477, 73c5f3a6b4):
    /// When addMissing is false, reservations of batches that are not available are skipped.
    /// </summary>
    public static void UpdateAvailableBatches(
        IDictionary<string, decimal> availableBatches,
        IEnumerable<KeyValuePair<string, decimal>> batchAdjustments,
        bool addMissing = true,
        int precision = 4)
    {
        foreach (var (batchNo, qty) in batchAdjustments)
        {
            if (availableBatches.TryGetValue(batchNo, out var currentQty))
            {
                availableBatches[batchNo] = Math.Round(currentQty + qty, precision);
            }
            else if (addMissing)
            {
                availableBatches[batchNo] = Math.Round(qty, precision);
            }
        }
    }

    /// <summary>
    /// Checks if batch validation (disabled or expired) is needed for a transaction.
    /// Per ERPNext PR #65127 (commits 260f861238, 5fd78f5a90, b3c2658f7f):
    /// - An invoice without Update Stock moves no stock, so its batches were checked when they moved (e.g. on Delivery Note).
    /// - A consolidated invoice repeats POS Invoices whose batches were already checked when sold; a batch disabled
    ///   or expired before the closing must not block the POS closing or consolidated invoice submission.
    /// - Non-invoices (Delivery Note, Purchase Receipt, Stock Entry) always require batch checks.
    /// </summary>
    public static bool IsBatchCheckNeeded(
        string voucherType,
        bool updateStock = true,
        bool isConsolidated = false)
    {
        if (isConsolidated)
        {
            return false;
        }

        var isInvoice = string.Equals(voucherType, "Sales Invoice", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(voucherType, "Purchase Invoice", StringComparison.OrdinalIgnoreCase);

        return !isInvoice || updateStock;
    }

    /// <summary>
    /// Validates whether a batch can be used in an outward transaction on a given posting date.
    /// Per ERPNext PR #65127: skips validation if IsBatchCheckNeeded returns false or if purpose is Material Issue / Material Transfer.
    /// </summary>
    public static void ValidateBatchUsable(
        string batchNo,
        DateTime? expiryDate,
        bool disabled,
        DateTime postingDate,
        string voucherType,
        bool updateStock = true,
        bool isConsolidated = false,
        bool isMaterialIssue = false)
    {
        if (!IsBatchCheckNeeded(voucherType, updateStock, isConsolidated))
        {
            return;
        }

        if (disabled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("batchNo", batchNo)
                .WithData("detail", $"Batch {batchNo} is disabled.");
        }

        if (!isMaterialIssue && expiryDate.HasValue && expiryDate.Value.Date < postingDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("batchNo", batchNo)
                .WithData("expiryDate", expiryDate.Value)
                .WithData("postingDate", postingDate)
                .WithData("detail", $"Batch {batchNo} has expired on {expiryDate.Value:yyyy-MM-dd}.");
        }
    }
}

/// <summary>
/// Pool of available serial numbers and batches available on an original transaction line for return allocation.
/// </summary>
public class ReturnSerialBatchPool
{
    public HashSet<string> SerialNos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<Guid, decimal> Batches { get; set; } = new();
    public Dictionary<string, decimal> BatchesByNo { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ReturnSerialBatchPool() { }

    public ReturnSerialBatchPool(
        IEnumerable<string>? serialNos = null,
        IDictionary<Guid, decimal>? batches = null,
        IDictionary<string, decimal>? batchesByNo = null)
    {
        if (serialNos != null)
        {
            foreach (var sn in serialNos)
            {
                if (!string.IsNullOrWhiteSpace(sn))
                    SerialNos.Add(sn.Trim());
            }
        }

        if (batches != null)
        {
            foreach (var (bId, qty) in batches)
                Batches[bId] = qty;
        }

        if (batchesByNo != null)
        {
            foreach (var (bNo, qty) in batchesByNo)
                BatchesByNo[bNo] = qty;
        }
    }
}

/// <summary>
/// Result of serial numbers and batches picked for a single return row.
/// </summary>
public class PickedSerialBatchResult
{
    public List<string> SerialNos { get; set; } = new();
    public Dictionary<Guid, decimal> Batches { get; set; } = new();
    public Dictionary<string, decimal> BatchesByNo { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
