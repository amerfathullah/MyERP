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
    /// Validates that all bundles referenced in the transaction match the expected company
    /// and do not belong to another draft voucher (ERPNext PR #58608 / PR #66994).
    /// </summary>
    public async Task ValidateBundleCompanyAsync(
        Guid expectedCompanyId,
        IEnumerable<Guid?> bundleIds,
        bool isInternalTransfer = false,
        string? expectedVoucherType = null,
        Guid? expectedVoucherId = null)
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

                // ERPNext PR #66994: a draft cannot take over the Serial and Batch Bundle of another draft voucher
                if (bundle.Status == Core.DocumentStatus.Draft
                    && expectedVoucherId.HasValue
                    && bundle.VoucherId != Guid.Empty
                    && (bundle.VoucherId != expectedVoucherId.Value
                        || (!string.IsNullOrWhiteSpace(expectedVoucherType)
                            && !string.Equals(bundle.VoucherType, expectedVoucherType, StringComparison.OrdinalIgnoreCase))))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("bundleId", bundle.Id)
                        .WithData("voucherType", bundle.VoucherType)
                        .WithData("voucherId", bundle.VoucherId)
                        .WithData("detail",
                            $"Serial and Batch Bundle {bundle.Id} belongs to draft {bundle.VoucherType} {bundle.VoucherId}. Select or create another bundle.");
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

    /// <summary>
    /// Resizes or removes the draft Serial and Batch Bundle of an internal transfer receipt row
    /// when its accepted or rejected stock quantity changes on a saved draft receipt.
    /// Per ERPNext commits 4c5fec17e2, 436188ea40, 27ea54fc6e, efd4a58548, 3d4ae9bb2e, 78a6ceb4a3, and f449a8fb26:
    /// - Return receipts (<paramref name="isReturn"/> = true) and non-internal receipts do not resize or remove transfer packages.
    /// - Compares stock qty (<c>qty * conversionFactor</c>) so UOM/conversion-factor changes resize the package even when transaction qty is unchanged.
    /// - When stock qty drops to 0 (accepted qty goes to 0 or rejected qty goes to 0), removes the draft package owned by the row (<c>VoucherDetailId == rowId</c>) and clears the row's bundle reference.
    /// - When stock qty changes to a positive value, resizes the single batch entry of the draft package owned by the row and recalculates bundle totals.
    /// </summary>
    public static DraftPackageSyncResult ResizeOrRemoveDraftInternalReceiptPackage(
        Guid rowId,
        decimal newQty,
        decimal newConversionFactor,
        decimal? oldQty,
        decimal? oldConversionFactor,
        SerialAndBatchBundle? package,
        bool isInternalReceipt = true,
        bool isReturn = false,
        bool isNewReceipt = false,
        int precision = 4)
    {
        if (!isInternalReceipt || isReturn || package == null)
        {
            return new DraftPackageSyncResult(package?.Id, Removed: false, Resized: false);
        }

        var factor = newConversionFactor > 0m ? newConversionFactor : 1m;
        var newStockQty = Math.Round(newQty * factor, precision);

        // If qty drops to 0 (accepted or rejected stock cleared), remove draft package owned by this row
        if (newStockQty == 0m)
        {
            if (package.Status == Core.DocumentStatus.Draft && package.VoucherDetailId == rowId)
            {
                package.Cancel();
                return new DraftPackageSyncResult(null, Removed: true, Resized: false);
            }

            return new DraftPackageSyncResult(package.Id, Removed: false, Resized: false);
        }

        if (isNewReceipt || !oldQty.HasValue)
        {
            return new DraftPackageSyncResult(package.Id, Removed: false, Resized: false);
        }

        var prevFactor = oldConversionFactor.HasValue && oldConversionFactor.Value > 0m
            ? oldConversionFactor.Value
            : 1m;
        var oldStockQty = Math.Round(oldQty.Value * prevFactor, precision);

        if (oldStockQty == newStockQty)
        {
            return new DraftPackageSyncResult(package.Id, Removed: false, Resized: false);
        }

        var nonSerialEntries = package.Entries
            .Where(e => string.IsNullOrWhiteSpace(e.SerialNo))
            .ToList();

        if (package.Status != Core.DocumentStatus.Draft
            || package.VoucherDetailId != rowId
            || nonSerialEntries.Count != 1)
        {
            return new DraftPackageSyncResult(package.Id, Removed: false, Resized: false);
        }

        var singleBatchEntry = nonSerialEntries[0];
        var sign = singleBatchEntry.Qty < 0m ? -1m : 1m;
        singleBatchEntry.Qty = sign * Math.Abs(newStockQty);
        package.Recalculate();

        return new DraftPackageSyncResult(package.Id, Removed: false, Resized: true);
    }

    /// <summary>
    /// Splits a Stock Entry row across multiple reserved batches while preserving
    /// <c>UseSerialBatchFields = true</c> on every overflow batch row.
    /// Per ERPNext commit 36fb8a7028:
    /// Overflow batch rows copied from the source row must have <c>UseSerialBatchFields = true</c>
    /// so subsequent validation does not re-auto-pick a batch without the reservation.
    /// </summary>
    public static List<ReservedBatchSplitRow> SplitStockEntryRowAcrossReservedBatches(
        Guid itemId,
        decimal requestedQty,
        IReadOnlyList<(Guid BatchId, string BatchNo, decimal AvailableReservedQty)> reservedBatches,
        int precision = 4)
    {
        var rows = new List<ReservedBatchSplitRow>();
        var remaining = Math.Round(requestedQty, precision);
        if (remaining <= 0m || reservedBatches == null || reservedBatches.Count == 0)
        {
            return rows;
        }

        var isFirst = true;
        foreach (var (batchId, batchNo, availableReservedQty) in reservedBatches)
        {
            if (remaining <= 0m)
                break;

            if (availableReservedQty <= 0m)
                continue;

            var allocQty = Math.Round(Math.Min(remaining, availableReservedQty), precision);
            if (allocQty <= 0m)
                continue;

            rows.Add(new ReservedBatchSplitRow(
                ItemId: itemId,
                BatchId: batchId,
                BatchNo: batchNo,
                Quantity: allocQty,
                UseSerialBatchFields: true,
                IsOverflowRow: !isFirst));

            remaining = Math.Round(remaining - allocQty, precision);
            isFirst = false;
        }

        return rows;
    }

    /// <summary>
    /// Resolves item rate and price list rate when a batch is auto-picked on an outward transaction row.
    /// Per ERPNext commit eba5799d6a:
    /// When a batch is auto-picked on a rate-locked row (e.g. linked to a source Sales Order row),
    /// keeps the source rate instead of overwriting with the batch-based item price.
    /// </summary>
    public static (decimal Rate, decimal PriceListRate) ResolveBatchAutoPickRate(
        decimal currentRate,
        decimal currentPriceListRate,
        decimal? batchBasedItemPrice,
        bool hasLockedRate)
    {
        if (!hasLockedRate && batchBasedItemPrice.HasValue && batchBasedItemPrice.Value != 0m)
        {
            return (batchBasedItemPrice.Value, batchBasedItemPrice.Value);
        }

        return (currentRate, currentPriceListRate);
    }

    /// <summary>
    /// Computes the stock quantity of each kept packed row for a Product Bundle component during re-pack / auto-pick.
    /// Per ERPNext commit 51a94d8372 (get_kept_component_rows / get_current_packed_items):
    /// Each kept stale packed row of a component packed from multiple Pick List locations takes its
    /// location's <c>packQty</c>, rather than every kept row taking the full component stock quantity.
    /// </summary>
    public static List<(Guid PackedRowId, decimal Qty)> GetKeptComponentPackedRowQuantities(
        IReadOnlyList<Guid> stalePackedRowIds,
        decimal componentStockQtyPerUnit,
        decimal bundleRowStockQty,
        IReadOnlyList<decimal>? pickListLocationPackQuantities = null,
        int precision = 4)
    {
        var result = new List<(Guid PackedRowId, decimal Qty)>();
        if (stalePackedRowIds == null || stalePackedRowIds.Count == 0)
        {
            return result;
        }

        var fullComponentQty = Math.Round(componentStockQtyPerUnit * bundleRowStockQty, precision);
        var remainingStale = new Queue<Guid>(stalePackedRowIds);

        if (pickListLocationPackQuantities != null && pickListLocationPackQuantities.Count > 0)
        {
            foreach (var locQty in pickListLocationPackQuantities)
            {
                if (remainingStale.Count == 0)
                    break;

                var packedRowId = remainingStale.Dequeue();
                result.Add((packedRowId, Math.Round(locQty, precision)));
            }
        }
        else if (remainingStale.Count > 0)
        {
            result.Add((remainingStale.Dequeue(), fullComponentQty));
        }

        return result;
    }
}

/// <summary>Result of syncing a draft internal receipt row's Serial and Batch Bundle.</summary>
public record DraftPackageSyncResult(
    Guid? BundleId,
    bool Removed,
    bool Resized);

/// <summary>Represents a Stock Entry row split across reserved batches (ERPNext commit 36fb8a7028).</summary>
public record ReservedBatchSplitRow(
    Guid ItemId,
    Guid BatchId,
    string BatchNo,
    decimal Quantity,
    bool UseSerialBatchFields,
    bool IsOverflowRow);

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
