using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Subcontracting business rules.
/// Handles SCO→SCR workflow, RM consumption tracking, and weighted average rate calculations.
/// Per ERPNext: subcontracting/doctype/subcontracting_order + subcontracting_controller.
/// </summary>
public class SubcontractingManager : DomainService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<SubcontractingReceipt, Guid> _scrRepository;

    public SubcontractingManager(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<SubcontractingReceipt, Guid> scrRepository)
    {
        _scoRepository = scoRepository;
        _scrRepository = scrRepository;
    }

    /// <summary>
    /// Validates a Subcontracting Order against its linked Purchase Order.
    /// Per ERPNext PR #59832 (commit 008fb0c12e):
    /// - Rejects if PO is Draft or Cancelled ("Please submit Purchase Order before proceeding").
    /// - Rejects if PO is Closed or OnHold ("Cannot create a Subcontracting Order against Purchase Order because it is Closed/On Hold").
    /// - Rejects if PO is already 100% received ("Cannot create more Subcontracting Orders against a fully received Purchase Order").
    /// - Rejects if any matched PO item row is closed ("Cannot create a Subcontracting Order for closed Purchase Order item").
    /// </summary>
    public void ValidateOrderAgainstPurchaseOrder(PurchaseOrder po, IEnumerable<Guid>? itemIds = null)
    {
        if (po.Status == DocumentStatus.Draft || po.Status == DocumentStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "PurchaseOrder")
                .WithData("status", po.Status.ToString())
                .WithData("detail", $"Please submit Purchase Order {po.OrderNumber} before proceeding.");
        }

        if (po.Status == DocumentStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed)
                .WithData("orderNumber", po.OrderNumber)
                .WithData("status", po.Status.ToString())
                .WithData("detail", $"Cannot create a Subcontracting Order against Purchase Order {po.OrderNumber} because it is {po.Status}.");
        }

        if (po.PerReceived >= 100m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverReceipt)
                .WithData("orderNumber", po.OrderNumber)
                .WithData("perReceived", po.PerReceived)
                .WithData("detail", $"Cannot create more Subcontracting Orders against a fully received Purchase Order {po.OrderNumber}.");
        }

        if (itemIds != null)
        {
            var claimedPoItemIds = new HashSet<Guid>();
            var rowIndex = 0;
            foreach (var itemId in itemIds)
            {
                rowIndex++;
                var matchingPoItems = po.Items.Where(i => i.ItemId == itemId).ToList();
                var poItem = matchingPoItems.FirstOrDefault(i => !claimedPoItemIds.Contains(i.Id))
                    ?? matchingPoItems.FirstOrDefault();

                if (poItem != null)
                {
                    if (poItem.IsClosed)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed)
                            .WithData("orderNumber", po.OrderNumber)
                            .WithData("itemId", itemId)
                            .WithData("detail", $"Cannot create a Subcontracting Order for closed Purchase Order item '{poItem.Description}'.");
                    }

                    // Per ERPNext commit 9ec94b2ae2: refuse two rows linked to the same order item row
                    if (!claimedPoItemIds.Add(poItem.Id))
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("orderNumber", po.OrderNumber)
                            .WithData("itemId", itemId)
                            .WithData("detail", $"Row {rowIndex}: Item '{poItem.Description}' is linked to the same Purchase Order Item row as an earlier row.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates a Subcontracting Receipt against its linked SCO.
    /// Ensures receipt qty doesn't exceed remaining ordered qty per item.
    /// </summary>
    public async Task ValidateReceiptAgainstOrderAsync(SubcontractingReceipt receipt)
    {
        var sco = await _scoRepository.GetAsync(receipt.SubcontractingOrderId);

        if (sco.Status == SubcontractingOrderStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "SubcontractingOrder")
                .WithData("status", sco.Status.ToString());
        }

        // Per ERPNext PR #58965: Project validation across subcontracting flow
        if (sco.ProjectId.HasValue && receipt.ProjectId.HasValue && sco.ProjectId.Value != receipt.ProjectId.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Subcontracting Receipt Project cannot differ from Subcontracting Order Project.");
        }

        foreach (var receiptItem in receipt.Items)
        {
            if (IsSecondaryItem(receiptItem)) continue;

            var scoItem = sco.Items.FirstOrDefault(i => i.ItemId == receiptItem.ItemId);
            if (scoItem == null) continue;

            if (scoItem.ProjectId.HasValue && receiptItem.ProjectId.HasValue && scoItem.ProjectId.Value != receiptItem.ProjectId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Subcontracting Receipt Item Project cannot differ from Subcontracting Order Item Project.");
            }

            var pendingQty = scoItem.Qty - scoItem.ReceivedQty;
            if (receiptItem.Qty > pendingQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverReceipt)
                    .WithData("item", receiptItem.ItemId)
                    .WithData("ordered", scoItem.Qty)
                    .WithData("received", scoItem.ReceivedQty)
                    .WithData("attempted", receiptItem.Qty);
            }
        }
    }

    /// <summary>
    /// Updates SCO received quantities when a SCR is submitted.
    /// Auto-transitions SCO status based on fulfillment percentage.
    /// Uses MIN(per-item %) formula for consistent completion detection.
    /// </summary>
    public async Task UpdateOrderOnReceiptAsync(SubcontractingReceipt receipt, bool reverse = false)
    {
        var sco = await _scoRepository.GetAsync(receipt.SubcontractingOrderId);

        foreach (var receiptItem in receipt.Items)
        {
            if (IsSecondaryItem(receiptItem)) continue;

            if (receipt.IsReturn && IsRejectedSubcontractingReturn(
                receiptItem.ReturnQtyFromRejectedWarehouse,
                receiptItem.WarehouseId,
                receiptItem.RejectedWarehouseId))
            {
                // Per ERPNext commit aeb5278994: return rows sending back rejected qty do not reduce accepted received qty on SCO
                continue;
            }

            var scoItem = sco.Items.FirstOrDefault(i => i.ItemId == receiptItem.ItemId);
            if (scoItem == null) continue;

            if (reverse)
                scoItem.ReceivedQty = Math.Max(0, scoItem.ReceivedQty - receiptItem.Qty);
            else
                scoItem.ReceivedQty += receiptItem.Qty;
        }

        // Calculate completion using MIN formula (bottleneck)
        if (sco.Items.Any())
        {
            var minPercentReceived = sco.Items.Min(i =>
                i.Qty > 0 ? (i.ReceivedQty / i.Qty * 100) : 100m);

            sco.PerReceived = Math.Round(minPercentReceived, 1);

            // Status transition
            if (sco.PerReceived >= 100)
            {
                sco.Close();
            }
            else if (sco.PerReceived > 0 && sco.Status == SubcontractingOrderStatus.Open)
            {
                sco.MarkPartiallyReceived();
            }
        }

        await _scoRepository.UpdateAsync(sco);
    }

    /// <summary>
    /// Calculates consumed RM quantities for a SCR based on proportional ratios.
    /// When backflushBasedOn is "Material Transferred for Subcontract" (and NOT a return),
    /// consumption is based on TransferredQty.
    /// Per ERPNext PR #46892 / commit 7479e1ec32: backflush setting is ignored on returns (isReturn = true),
    /// always falling back to BOM/RequiredQty.
    /// Per ERPNext PR #66545 / commit 35de04d499: partial returns of serialized or whole-number UOM
    /// raw materials round returned units up to whole units (capped at consumed/required qty).
    /// </summary>
    public SubcontractingRmConsumption[] CalculateRmConsumption(
        SubcontractingOrder sco,
        decimal receivedFgQty,
        string? backflushBasedOn = "BOM",
        bool isReturn = false,
        IReadOnlySet<Guid>? wholeUnitRmItemIds = null)
    {
        // Total FG qty ordered
        var totalFgQty = sco.Items.Sum(i => i.Qty);
        if (totalFgQty <= 0) return Array.Empty<SubcontractingRmConsumption>();

        var ratio = receivedFgQty / totalFgQty;
        var useTransferred = !isReturn && string.Equals(backflushBasedOn, "Material Transferred for Subcontract", StringComparison.OrdinalIgnoreCase);

        return sco.SuppliedItems
            .Select(si =>
            {
                var baseQty = useTransferred ? si.TransferredQty : si.RequiredQty;
                var consumedQty = Math.Round(baseQty * ratio, 4);
                if (isReturn && wholeUnitRmItemIds != null && wholeUnitRmItemIds.Contains(si.ItemId) && consumedQty < 0m)
                {
                    var ceilingQty = Math.Ceiling(Math.Abs(consumedQty));
                    var maxReturnable = si.ConsumedQty > 0m ? si.ConsumedQty : si.RequiredQty;
                    consumedQty = -Math.Min(ceilingQty, maxReturnable);
                }
                return new SubcontractingRmConsumption
                {
                    ItemId = si.ItemId,
                    RequiredQty = si.RequiredQty,
                    ConsumedQty = consumedQty,
                    WarehouseId = si.ReserveWarehouseId,
                    BatchId = si.BatchId
                };
            })
            .ToArray();
    }

    /// <summary>
    /// Computes raw material return quantities (negative ConsumedQty = stock returned to supplier warehouse)
    /// from what the original Subcontracting Receipt actually consumed, in proportion to returned FG qty.
    /// Per ERPNext PR #66545:
    /// - Returns the RM quantities that the receipt actually consumed (ignoring current BOM/backflush changes).
    /// - Caps cumulative RM returns across all non-cancelled returns at what the original receipt consumed.
    /// - Never consumes raw materials on a Subcontract Return (ConsumedQty &lt;= 0m).
    /// - Rounds partial returns of whole-number UOM or serialized RM items up to whole units (capped at remaining).
    /// </summary>
    public static SubcontractingRmConsumption[] CalculateReturnRmFromReceipt(
        IReadOnlyList<(Guid ItemId, Guid? BatchId, Guid? WarehouseId, decimal ConsumedQty)> originalConsumedRms,
        decimal originalReceiptFgQty,
        decimal returnReceiptFgQty,
        IReadOnlyDictionary<(Guid ItemId, Guid? BatchId), decimal>? priorReturnedByRm = null,
        IReadOnlySet<Guid>? wholeUnitRmItemIds = null)
    {
        var returnedFgAbs = Math.Abs(returnReceiptFgQty);
        if (originalReceiptFgQty <= 0m || returnedFgAbs <= 0m || originalConsumedRms.Count == 0)
        {
            return Array.Empty<SubcontractingRmConsumption>();
        }

        var ratio = returnedFgAbs / originalReceiptFgQty;
        var returnedTracker = priorReturnedByRm != null
            ? new Dictionary<(Guid ItemId, Guid? BatchId), decimal>(priorReturnedByRm)
            : new Dictionary<(Guid ItemId, Guid? BatchId), decimal>();

        var result = new List<SubcontractingRmConsumption>(originalConsumedRms.Count);
        foreach (var rm in originalConsumedRms)
        {
            if (rm.ConsumedQty <= 0m)
            {
                continue;
            }

            var key = (rm.ItemId, rm.BatchId);
            var priorReturned = returnedTracker.GetValueOrDefault(key, 0m);
            var remainingReturnable = Math.Max(0m, Math.Round(rm.ConsumedQty - priorReturned, 4, MidpointRounding.AwayFromZero));
            if (remainingReturnable <= 0m)
            {
                continue;
            }

            var targetReturn = Math.Round(rm.ConsumedQty * ratio, 4, MidpointRounding.AwayFromZero);
            if (wholeUnitRmItemIds != null && wholeUnitRmItemIds.Contains(rm.ItemId) && targetReturn > 0m)
            {
                targetReturn = Math.Ceiling(targetReturn);
            }

            var cappedReturn = Math.Max(0m, Math.Min(targetReturn, remainingReturnable));
            if (cappedReturn <= 0m)
            {
                continue;
            }

            returnedTracker[key] = priorReturned + cappedReturn;
            result.Add(new SubcontractingRmConsumption
            {
                ItemId = rm.ItemId,
                RequiredQty = rm.ConsumedQty,
                ConsumedQty = -cappedReturn,
                WarehouseId = rm.WarehouseId,
                BatchId = rm.BatchId
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Determines whether a Subcontracting Receipt return row sends back rejected qty.
    /// Per ERPNext commit aeb5278994:
    /// A Subcontracting Receipt return row sends back rejected qty ONLY while it is flagged
    /// (ReturnQtyFromRejectedWarehouse = true) AND its warehouse matches the original row's RejectedWarehouseId.
    /// </summary>
    public static bool IsRejectedSubcontractingReturn(
        bool returnQtyFromRejectedWarehouse,
        Guid? returnWarehouseId,
        Guid? originalRejectedWarehouseId)
    {
        return returnQtyFromRejectedWarehouse
            && returnWarehouseId.HasValue
            && originalRejectedWarehouseId.HasValue
            && returnWarehouseId.Value == originalRejectedWarehouseId.Value;
    }

    /// <summary>
    /// Identifies secondary items (Co-Product, By-Product, Scrap, Additional Finished Good) on a Subcontracting Receipt.
    /// </summary>
    public static bool IsSecondaryItem(SubcontractingReceiptItem item)
    {
        return !string.IsNullOrWhiteSpace(item.SecondaryItemType)
            || item.ValuationType.HasValue
            || item.BomSecondaryItemId.HasValue;
    }

    /// <summary>
    /// Calculates the secondary item quantity to include on a Subcontracting Receipt return row.
    /// Per ERPNext PR #66400 (commits d3b20f2ccf, aa24a4f7e8, 7e75d6acdc):
    /// - Partial returns round on the running finished-good return total so parts add up cleanly.
    /// - Returns that round to zero are omitted (returns 0m).
    /// - The final return covering all remaining finished-good qty takes the exact secondary qty left.
    /// </summary>
    public static decimal CalculateReturnSecondaryQty(
        decimal originalFgQty,
        decimal originalSecondaryQty,
        decimal priorReturnedFgQty,
        decimal currentReturnFgQty,
        decimal priorReturnedSecondaryQty,
        int precision = 4)
    {
        if (originalFgQty <= 0m || originalSecondaryQty <= 0m || currentReturnFgQty <= 0m)
        {
            return 0m;
        }

        if (Math.Round(priorReturnedFgQty + currentReturnFgQty, precision) >= Math.Round(originalFgQty, precision))
        {
            return Math.Max(0m, Math.Round(originalSecondaryQty - priorReturnedSecondaryQty, precision));
        }

        var perUnit = originalSecondaryQty / originalFgQty;
        var before = Math.Round(priorReturnedFgQty * perUnit, precision);
        var after = Math.Round((priorReturnedFgQty + currentReturnFgQty) * perUnit, precision);
        var delta = Math.Round(after - before, precision);
        return delta > 0m ? delta : 0m;
    }

    /// <summary>
    /// Resolves the original entry's warehouse that a Subcontracting Receipt return row goes back at.
    /// Per ERPNext commit 7a711015db (get_subcontracting_return_warehouse):
    /// If IsRejectedSubcontractingReturn is true, returns originalRejectedWarehouseId;
    /// otherwise returns originalAcceptedWarehouseId.
    /// </summary>
    public static Guid? GetSubcontractingReturnWarehouse(
        bool returnQtyFromRejectedWarehouse,
        Guid? returnWarehouseId,
        Guid? originalAcceptedWarehouseId,
        Guid? originalRejectedWarehouseId)
    {
        if (IsRejectedSubcontractingReturn(returnQtyFromRejectedWarehouse, returnWarehouseId, originalRejectedWarehouseId))
        {
            return originalRejectedWarehouseId;
        }

        return originalAcceptedWarehouseId;
    }

    /// <summary>
    /// Checks whether a Stock Ledger Entry represents raw material brought back by a Subcontracting Receipt return.
    /// Per ERPNext PR #64797 / commit 51e5ba7ec9: spots returned supplied items without extra database queries
    /// by verifying voucher type, positive incoming quantity, and presence of DependantSleVoucherDetailNo.
    /// </summary>
    public static bool IsReturnedSuppliedItem(string? voucherType, decimal actualQty, Guid? dependantSleVoucherDetailNo)
    {
        return (string.Equals(voucherType, "SubcontractingReceipt", StringComparison.OrdinalIgnoreCase)
             || string.Equals(voucherType, "Subcontracting Receipt", StringComparison.OrdinalIgnoreCase))
            && actualQty > 0
            && dependantSleVoucherDetailNo.HasValue
            && dependantSleVoucherDetailNo.Value != Guid.Empty;
    }

    /// <summary>
    /// Overload for StockLedgerEntry entity.
    /// </summary>
    public static bool IsReturnedSuppliedItem(StockLedgerEntry sle)
    {
        return IsReturnedSuppliedItem(sle.VoucherType, sle.QuantityChange, sle.DependantSleVoucherDetailNo);
    }

    /// <summary>
    /// Resolves item, warehouse, and transaction type for supplied item serial/batch bundle handling.
    /// Per ERPNext PR #64867 (commits 1bcdf9c96f and 7b124a874c):
    /// Supplied-item rows keep the raw material item in `rm_item_code` and the warehouse on the parent's `supplier_warehouse`.
    /// On standard receipt, supplied items are Outward (consumed from subcontractor warehouse);
    /// on return receipt (isReturn = true), supplied items are Inward (returned to subcontractor warehouse).
    /// </summary>
    public static (Guid ItemId, Guid WarehouseId, string TransactionType) ResolveSuppliedItemBundleParameters(
        Guid suppliedItemId,
        Guid supplierWarehouseId,
        bool isReturn)
    {
        return (
            ItemId: suppliedItemId,
            WarehouseId: supplierWarehouseId,
            TransactionType: isReturn ? "Inward" : "Outward"
        );
    }

    /// <summary>
    /// Computes the quantity-weighted rate that the returned Subcontracting Receipt consumed the row's serial nos or batches at.
    /// Per ERPNext PR #64861 (commits c28d1f5bc6, 7b7bfac7f8, c3ece0832e, 1dfb128847, eb0ee73b9d):
    /// Returns the weighted average rate across the consumed batches/serials, or null if no matching consumed items.
    /// </summary>
    public static decimal? GetSerialBatchReturnRate(
        IEnumerable<(Guid? BatchId, string? SerialNo, decimal Qty)> returnedEntries,
        IReadOnlyDictionary<(Guid? BatchId, string? SerialNo), decimal> consumedRates,
        int precision = 4)
    {
        var validReturned = new List<(decimal Rate, decimal Qty)>();
        foreach (var entry in returnedEntries)
        {
            var key = (entry.BatchId, entry.SerialNo?.Trim().ToUpperInvariant());
            if (consumedRates.TryGetValue(key, out var rate))
            {
                validReturned.Add((rate, Math.Abs(entry.Qty)));
            }
            else if (entry.BatchId.HasValue && consumedRates.TryGetValue((entry.BatchId, null), out var batchRate))
            {
                validReturned.Add((batchRate, Math.Abs(entry.Qty)));
            }
            else if (!string.IsNullOrWhiteSpace(entry.SerialNo) &&
                     consumedRates.TryGetValue((null, entry.SerialNo.Trim().ToUpperInvariant()), out var serialRate))
            {
                validReturned.Add((serialRate, Math.Abs(entry.Qty)));
            }
        }

        var totalQty = validReturned.Sum(x => x.Qty);
        if (totalQty <= 0) return null;

        var totalValue = validReturned.Sum(x => x.Rate * x.Qty);
        return Math.Round(totalValue / totalQty, precision);
    }

    /// <summary>
    /// Computes the quantity-weighted rate that the returned Subcontracting Receipt consumed the row's serial nos or batches at (by batch number).
    /// Per ERPNext PR #64861 (commits eb0ee73b9d, c28d1f5bc6):
    /// Returns the weighted average rate across the consumed batches/serials, or null if no matching consumed items.
    /// </summary>
    public static decimal? GetSerialBatchReturnRate(
        IEnumerable<(string? BatchNo, string? SerialNo, decimal Qty)> returnedEntries,
        IReadOnlyDictionary<(string? BatchNo, string? SerialNo), decimal> consumedRates,
        int precision = 4)
    {
        var validReturned = new List<(decimal Rate, decimal Qty)>();
        foreach (var entry in returnedEntries)
        {
            var sn = entry.SerialNo?.Trim();
            var bn = entry.BatchNo?.Trim();
            var key = (bn, sn?.ToUpperInvariant());
            if (consumedRates.TryGetValue(key, out var rate))
            {
                validReturned.Add((rate, Math.Abs(entry.Qty)));
            }
            else if (!string.IsNullOrEmpty(bn) && consumedRates.TryGetValue((bn, null), out var batchRate))
            {
                validReturned.Add((batchRate, Math.Abs(entry.Qty)));
            }
            else if (!string.IsNullOrWhiteSpace(sn) &&
                     consumedRates.TryGetValue((null, sn.ToUpperInvariant()), out var serialRate))
            {
                validReturned.Add((serialRate, Math.Abs(entry.Qty)));
            }
        }

        var totalQty = validReturned.Sum(x => x.Qty);
        if (totalQty <= 0) return null;

        var totalValue = validReturned.Sum(x => x.Rate * x.Qty);
        return Math.Round(totalValue / totalQty, precision);
    }

    /// <summary>
    /// Computes the return rate for a supplied item row that directly specifies batch_no or serial_no strings.
    /// Per ERPNext PR #64861 (commits eb0ee73b9d, c28d1f5bc6):
    /// Matches serial numbers case-insensitively, or matches batch.
    /// </summary>
    public static decimal? GetSerialBatchReturnRate(
        string? batchNo,
        string? serialNos,
        decimal qty,
        IReadOnlyDictionary<(string? BatchNo, string? SerialNo), decimal> consumedRates,
        int precision = 4)
    {
        var entries = new List<(string? BatchNo, string? SerialNo, decimal Qty)>();
        if (!string.IsNullOrWhiteSpace(serialNos))
        {
            var sns = serialNos.Split(new[] { '\n', ',', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var sn in sns)
            {
                entries.Add((batchNo, sn, 1m));
            }
        }
        else if (!string.IsNullOrWhiteSpace(batchNo))
        {
            entries.Add((batchNo, null, Math.Abs(qty)));
        }

        return GetSerialBatchReturnRate(entries, consumedRates, precision);
    }

    /// <summary>
    /// Resolves the valuation rate for a supplied item on return or standard subcontracting receipt.
    /// Per ERPNext PR #64861 (commits eb0ee73b9d, c28d1f5bc6):
    /// - If isReturn is true: returns the qty-weighted consumed rate of the picked serial/batches in its bundle,
    ///   falling back to original receipt's consumed rate. It is NOT overwritten by current item standard cost.
    /// - If isReturn is false: uses the incoming standard valuation rate.
    /// </summary>
    public static decimal CalculateSuppliedItemRate(
        bool isReturn,
        decimal? serialBatchConsumedRate,
        decimal? originalConsumedRate,
        decimal standardCostRate)
    {
        if (!isReturn)
        {
            return standardCostRate;
        }

        if (serialBatchConsumedRate.HasValue && serialBatchConsumedRate.Value > 0)
        {
            return serialBatchConsumedRate.Value;
        }

        if (originalConsumedRate.HasValue && originalConsumedRate.Value > 0)
        {
            return originalConsumedRate.Value;
        }

        return standardCostRate;
    }

    /// <summary>
    /// Splits a multiline or comma-separated serial number string into trimmed, distinct serial numbers (case-insensitive order preserved).
    /// </summary>
    public static List<string> ParseSerialNos(string? serialNoText)
    {
        if (string.IsNullOrWhiteSpace(serialNoText))
        {
            return new List<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = serialNoText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (seen.Add(part))
            {
                result.Add(part);
            }
        }

        return result;
    }

    /// <summary>
    /// Collects all serial numbers currently held across existing supplied item rows so subsequent rows
    /// for the same raw material item do not re-pick already assigned serial numbers.
    /// Per ERPNext PR #66525 (get_used_serial_nos).
    /// </summary>
    public static HashSet<string> GetUsedSerialNos(IEnumerable<string?> serialNoFields)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in serialNoFields)
        {
            foreach (var sn in ParseSerialNos(field))
            {
                used.Add(sn);
            }
        }

        return used;
    }

    /// <summary>
    /// Picks up to <paramref name="requiredCount"/> available serial numbers for a supplied item row,
    /// skipping serial numbers already held in <paramref name="usedSerialNos"/> and recording newly picked serials.
    /// Per ERPNext PR #66525 (test_supplied_row_skips_serial_nos_held_by_another_row).
    /// </summary>
    public static List<string> PickAvailableSerialNosForRow(
        IEnumerable<string> availableSerialNos,
        ISet<string> usedSerialNos,
        int requiredCount)
    {
        if (requiredCount <= 0)
        {
            return new List<string>();
        }

        var picked = new List<string>(requiredCount);
        foreach (var rawSn in availableSerialNos)
        {
            if (picked.Count >= requiredCount)
            {
                break;
            }

            var sn = rawSn?.Trim();
            if (string.IsNullOrEmpty(sn) || usedSerialNos.Contains(sn))
            {
                continue;
            }

            if (usedSerialNos.Add(sn))
            {
                picked.Add(sn);
            }
        }

        return picked;
    }

    /// <summary>
    /// Computes net finished good valuation rate after deducting allocated secondary items cost per unit.
    /// Per ERPNext PR #66571.
    /// </summary>
    public static decimal CalculateFinishedGoodRateWithSecondaryCost(
        decimal serviceCostPerQty,
        decimal rawMaterialCostPerQty,
        decimal secondaryItemsCostPerQty,
        int precision = 4)
    {
        return Math.Max(0m, Math.Round(serviceCostPerQty + rawMaterialCostPerQty - secondaryItemsCostPerQty, precision));
    }

    /// <summary>
    /// Resolves finished good rate for a Subcontracting Receipt return using the original receipt's
    /// service cost and secondary items cost per unit (rather than current item valuation rates),
    /// while zeroing service cost on rejected returns.
    /// Per ERPNext PR #66571 (get_returned_costs).
    /// </summary>
    public static decimal ResolveReturnedFinishedGoodRate(
        bool isRejectedReturn,
        decimal receiptServiceCostPerQty,
        decimal receiptRmCostPerQty,
        decimal receiptSecondaryItemsCostPerQty,
        decimal fallbackReceiptRate = 0m,
        int precision = 4)
    {
        if (isRejectedReturn)
        {
            return 0m;
        }

        if (receiptServiceCostPerQty != 0m || receiptRmCostPerQty != 0m || receiptSecondaryItemsCostPerQty != 0m)
        {
            return CalculateFinishedGoodRateWithSecondaryCost(
                receiptServiceCostPerQty,
                receiptRmCostPerQty,
                receiptSecondaryItemsCostPerQty,
                precision);
        }

        return Math.Round(fallbackReceiptRate, precision);
    }

    /// <summary>
    /// Computes the allocated rate for a percentage-based secondary item on a Subcontracting Receipt.
    /// Per ERPNext commit 7e66f8e926: returns 0m when <paramref name="secondaryQty"/> rounds to 0
    /// instead of throwing a DivideByZeroException.
    /// </summary>
    public static decimal CalculatePercentageSecondaryRate(
        decimal fgReceivedQty,
        decimal rmCostPerQty,
        decimal serviceCostPerQty,
        decimal additionalCostPerQty,
        decimal lcvAmount,
        decimal ownSecondaryCost,
        decimal costAllocationPercentage,
        decimal secondaryQty,
        int precision = 4)
    {
        if (secondaryQty == 0m || fgReceivedQty == 0m || costAllocationPercentage <= 0m)
        {
            return 0m;
        }

        var lcvCostPerQty = lcvAmount / fgReceivedQty;
        var fgItemCost = ((rmCostPerQty + additionalCostPerQty + lcvCostPerQty + serviceCostPerQty) * fgReceivedQty) - ownSecondaryCost;
        return Math.Round((fgItemCost * (costAllocationPercentage / 100m)) / secondaryQty, precision);
    }

    /// <summary>
    /// Resolves the rate of a secondary item row on a Subcontracting Receipt or Subcontracting Return.
    /// Per ERPNext commits e076a8adac, a1169be24c, and 7e66f8e926:
    /// - A return row whose quantity rounds to 0 skips pricing (returns 0m without invoking valuation lookup).
    /// - A receipt row whose quantity rounds to 0 still prices Manual / ValuationRate rows (keeping the rate for when process loss is lowered),
    ///   while PercentageOfComponentCost rows with 0 qty return 0m.
    /// </summary>
    public static decimal ResolveSecondaryItemRate(
        bool isReturn,
        decimal secondaryQty,
        Manufacturing.SecondaryItemValuationType valuationType,
        decimal bomSecondaryBaseCost,
        decimal bomSecondaryStockQty,
        Func<decimal>? resolveWarehouseValuationRate,
        decimal fgReceivedQty = 0m,
        decimal rmCostPerQty = 0m,
        decimal serviceCostPerQty = 0m,
        decimal additionalCostPerQty = 0m,
        decimal lcvAmount = 0m,
        decimal ownSecondaryCost = 0m,
        decimal costAllocationPercentage = 0m,
        int precision = 4)
    {
        if (isReturn && secondaryQty == 0m)
        {
            return 0m;
        }

        if (valuationType == Manufacturing.SecondaryItemValuationType.Manual)
        {
            return bomSecondaryStockQty != 0m
                ? Math.Round(bomSecondaryBaseCost / bomSecondaryStockQty, precision)
                : 0m;
        }

        if (valuationType == Manufacturing.SecondaryItemValuationType.ValuationRate)
        {
            var warehouseRate = resolveWarehouseValuationRate != null ? resolveWarehouseValuationRate() : 0m;
            if (warehouseRate == 0m && bomSecondaryStockQty != 0m)
            {
                warehouseRate = Math.Round(bomSecondaryBaseCost / bomSecondaryStockQty, precision);
            }
            return warehouseRate;
        }

        return CalculatePercentageSecondaryRate(
            fgReceivedQty,
            rmCostPerQty,
            serviceCostPerQty,
            additionalCostPerQty,
            lcvAmount,
            ownSecondaryCost,
            costAllocationPercentage,
            secondaryQty,
            precision);
    }

    /// <summary>
    /// Computes secondary item quantities for a Subcontracting Return per ERPNext commit 4b9002b2a4:
    /// - Keeps a secondary row even when its proportional return share initially rounds to 0,
    ///   so that when the return takes the last remaining finished-goods quantity (isLastFgReturn == true),
    ///   set_remaining_secondary_qty assigns the remaining unreturned secondary balance
    ///   (PriorReturnedQty - OriginalSecondaryQty).
    /// - Filters out only rows whose final effective Qty (ReceivedQty - ProcessLossQty) is still 0 after
    ///   that remaining-balance adjustment.
    /// </summary>
    public static List<(Guid SourceSecondaryRowId, Guid ItemId, decimal ReceivedQty, decimal ProcessLossQty, decimal Qty)> ResolveReturnSecondaryItemQuantities(
        decimal returnFgRatio,
        bool isLastFgReturn,
        IEnumerable<(Guid SourceSecondaryRowId, Guid ItemId, decimal OriginalReceivedQty, decimal OriginalProcessLossQty, decimal OriginalQty, decimal PriorReturnedReceivedQty, decimal PriorReturnedProcessLossQty, decimal PriorReturnedQty)> sourceSecondaryRows,
        int precision = 2)
    {
        var result = new List<(Guid SourceSecondaryRowId, Guid ItemId, decimal ReceivedQty, decimal ProcessLossQty, decimal Qty)>();
        if (sourceSecondaryRows == null)
        {
            return result;
        }

        var absRatio = -Math.Abs(returnFgRatio);
        foreach (var row in sourceSecondaryRows)
        {
            decimal receivedQty;
            decimal processLossQty;
            decimal qty;

            if (isLastFgReturn)
            {
                // In ERPNext, prior returned quantities are negative, so (priorReturnedQty - originalQty)
                // is negative when callers pass positive priorReturnedQty, or we normalize magnitudes here:
                var priorReturnedReceivedMag = Math.Abs(row.PriorReturnedReceivedQty);
                var priorReturnedLossMag = Math.Abs(row.PriorReturnedProcessLossQty);
                var priorReturnedQtyMag = Math.Abs(row.PriorReturnedQty);

                receivedQty = Math.Round(priorReturnedReceivedMag - row.OriginalReceivedQty, precision, MidpointRounding.AwayFromZero);
                processLossQty = Math.Round(priorReturnedLossMag - row.OriginalProcessLossQty, precision, MidpointRounding.AwayFromZero);
                qty = Math.Round(priorReturnedQtyMag - row.OriginalQty, precision, MidpointRounding.AwayFromZero);
            }
            else
            {
                receivedQty = Math.Round(row.OriginalReceivedQty * absRatio, precision, MidpointRounding.AwayFromZero);
                processLossQty = Math.Round(row.OriginalProcessLossQty * absRatio, precision, MidpointRounding.AwayFromZero);
                qty = Math.Round(receivedQty - processLossQty, precision, MidpointRounding.AwayFromZero);
            }

            if (qty != 0m)
            {
                result.Add((row.SourceSecondaryRowId, row.ItemId, receivedQty, processLossQty, qty));
            }
        }

        return result;
    }
}

public class SubcontractingRmConsumption
{
    public Guid ItemId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
}
