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
    /// Validates that no unreceived Subcontracting Order row points to a closed Purchase Order or closed PO row.
    /// Per ERPNext PR #67362 (validate_purchase_order_rows_are_open): a closed PO row cannot be reopened/processed
    /// via a Subcontracting Order while qty remains to be received.
    /// </summary>
    public void ValidatePurchaseOrderRowsAreOpen(PurchaseOrder po, IEnumerable<SubcontractingOrderItem> scoItems)
    {
        if (po.Status == DocumentStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed)
                .WithData("orderNumber", po.OrderNumber)
                .WithData("status", po.Status.ToString())
                .WithData("detail", $"Purchase Order {po.OrderNumber} is closed and linked Subcontracting Order cannot be reopened.");
        }

        var rowIndex = 0;
        foreach (var scoItem in scoItems)
        {
            rowIndex++;
            if (scoItem.ReceivedQty >= scoItem.Qty)
                continue;

            var poItem = po.Items.FirstOrDefault(i => i.ItemId == scoItem.ItemId);
            if (poItem != null && poItem.IsClosed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed)
                    .WithData("orderNumber", po.OrderNumber)
                    .WithData("itemId", scoItem.ItemId)
                    .WithData("detail", $"Row #{rowIndex}: Item '{poItem.Description}' is closed in Purchase Order {po.OrderNumber} and cannot be processed further.");
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
    /// Per ERPNext commits e076a8adac, a1169be24c, 7e66f8e926, 49411efce2, 2e0c35f11b, and ef3b45ff13:
    /// - A return row whose quantity rounds to 0 skips pricing (returns 0m without invoking valuation lookup).
    /// - A non-zero return row (<c>ValuationRate</c>, <c>Manual</c>, or <c>PercentageOfComponentCost</c>) returns at its
    ///   original receipt rate (<paramref name="originalReceiptSecondaryRate"/>) when available so it matches the stock ledger.
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
        int precision = 4,
        decimal? originalReceiptSecondaryRate = null)
    {
        if (isReturn && secondaryQty == 0m)
        {
            return 0m;
        }

        if (isReturn && originalReceiptSecondaryRate.HasValue)
        {
            return Math.Round(originalReceiptSecondaryRate.Value, precision, MidpointRounding.AwayFromZero);
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

    /// <summary>
    /// Validates on submit of a Subcontracting Receipt Return that cumulative returned raw materials
    /// (earlier submitted returns plus the current return) do not exceed what the original receipt consumed
    /// per ERPNext commit b354d3f1c3 (validate_returned_raw_materials).
    /// </summary>
    public static void ValidateReturnedRawMaterialsDoNotExceedConsumed(
        string returnAgainstReceiptNumber,
        IReadOnlyDictionary<(Guid ReceiptItemRowId, Guid RmItemId), (decimal OriginalConsumedQty, decimal PriorReturnedQty, string? RmItemCode)> returnableMaterials,
        IEnumerable<(Guid? ReceiptItemRowId, Guid RmItemId, decimal CurrentReturnConsumedQty)> currentReturnSuppliedRows,
        int precision = 4)
    {
        if (returnableMaterials == null || currentReturnSuppliedRows == null)
        {
            return;
        }

        var currentByKey = new Dictionary<(Guid ReceiptItemRowId, Guid RmItemId), decimal>();
        foreach (var row in currentReturnSuppliedRows)
        {
            if (!row.ReceiptItemRowId.HasValue || row.ReceiptItemRowId.Value == Guid.Empty)
            {
                continue;
            }

            var key = (row.ReceiptItemRowId.Value, row.RmItemId);
            currentByKey[key] = currentByKey.GetValueOrDefault(key, 0m) + Math.Abs(row.CurrentReturnConsumedQty);
        }

        foreach (var (key, currentQty) in currentByKey)
        {
            if (returnableMaterials.TryGetValue(key, out var mat))
            {
                var excess = Math.Round(mat.PriorReturnedQty + currentQty - mat.OriginalConsumedQty, precision, MidpointRounding.AwayFromZero);
                if (excess > 0m)
                {
                    var rmLabel = !string.IsNullOrWhiteSpace(mat.RmItemCode) ? mat.RmItemCode : key.RmItemId.ToString();
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Returns of {returnAgainstReceiptNumber} would take back more of raw material {rmLabel} than it consumed. Use Reset Raw Materials Table to pick the raw materials again.");
                }
            }
        }
    }

    /// <summary>
    /// Determines whether BOM required quantity validation should be skipped on a Subcontracting Receipt
    /// per ERPNext commit 2da4575f47 (validate_bom_required_qty):
    /// Always skips on a Subcontracting Return (which takes back what the receipt consumed, even if it exceeded
    /// or differed from the BOM), or when backflushing is based on Material Transferred and ValidateConsumedQty is disabled.
    /// </summary>
    public static bool ShouldSkipBomRequiredQtyValidation(
        bool isReturn,
        string? backflushBasedOn,
        bool validateConsumedQty)
    {
        if (isReturn)
        {
            return true;
        }

        return string.Equals(backflushBasedOn, "Material Transferred for Subcontract", StringComparison.OrdinalIgnoreCase)
            && !validateConsumedQty;
    }

    /// <summary>
    /// Pairs a Subcontracting Return's secondary rows with the original receipt's secondary rows
    /// per ERPNext commits 2f92fcf85f, 0263dc3b72, and 4f03188e04 (set_return_secondary_references / pair_legacy_secondary_rows):
    /// - Where the original receipt secondary row has a <c>BomSecondaryItemId</c>, pairs by <c>(FgReceiptItemId, BomSecondaryItemId)</c>.
    /// - For legacy pre-v16 receipt secondary rows without <c>BomSecondaryItemId</c>, pairs receipt rows of each <c>ItemId</c>
    ///   with the BOM's secondary rows of that <c>ItemId</c> by ascending quantity rank (<c>Qty</c> vs <c>BomStockQty</c>).
    /// </summary>
    public static Dictionary<(Guid FgReceiptItemId, Guid BomSecondaryItemId), Guid> PairReturnSecondaryRowsWithOriginalReceiptRows(
        IEnumerable<(Guid OriginalSecondaryRowId, Guid FgReceiptItemId, Guid ItemId, Guid? BomSecondaryItemId, decimal Qty)> originalSecondaryRows,
        IReadOnlyDictionary<Guid, IReadOnlyList<(Guid BomSecondaryItemId, Guid ItemId, decimal StockQty)>>? bomSecondaryRowsByFgReceiptItemId = null)
    {
        var map = new Dictionary<(Guid FgReceiptItemId, Guid BomSecondaryItemId), Guid>();
        if (originalSecondaryRows == null)
        {
            return map;
        }

        var legacyByFgRow = new Dictionary<Guid, List<(Guid OriginalSecondaryRowId, Guid ItemId, decimal Qty)>>();
        foreach (var row in originalSecondaryRows)
        {
            if (row.BomSecondaryItemId.HasValue && row.BomSecondaryItemId.Value != Guid.Empty)
            {
                map[(row.FgReceiptItemId, row.BomSecondaryItemId.Value)] = row.OriginalSecondaryRowId;
            }
            else
            {
                if (!legacyByFgRow.TryGetValue(row.FgReceiptItemId, out var list))
                {
                    list = new List<(Guid, Guid, decimal)>();
                    legacyByFgRow[row.FgReceiptItemId] = list;
                }
                list.Add((row.OriginalSecondaryRowId, row.ItemId, row.Qty));
            }
        }

        if (bomSecondaryRowsByFgReceiptItemId != null)
        {
            foreach (var (fgReceiptItemId, legacyRows) in legacyByFgRow)
            {
                if (!bomSecondaryRowsByFgReceiptItemId.TryGetValue(fgReceiptItemId, out var bomRows) || bomRows == null)
                {
                    continue;
                }

                foreach (var itemId in legacyRows.Select(r => r.ItemId).Distinct())
                {
                    var receiptByQty = legacyRows
                        .Where(r => r.ItemId == itemId)
                        .OrderBy(r => r.Qty)
                        .ToList();
                    var bomByQty = bomRows
                        .Where(b => b.ItemId == itemId)
                        .OrderBy(b => b.StockQty)
                        .ToList();

                    var pairCount = Math.Min(receiptByQty.Count, bomByQty.Count);
                    for (var i = 0; i < pairCount; i++)
                    {
                        map[(fgReceiptItemId, bomByQty[i].BomSecondaryItemId)] = receiptByQty[i].OriginalSecondaryRowId;
                    }
                }
            }
        }

        return map;
    }

    /// <summary>
    /// Rebuilds the <c>TransferredQty</c> on a Subcontracting Order's Stock Reservation Entries from the
    /// remaining active "Send to Subcontractor" transfers when one transfer is cancelled
    /// per ERPNext commits 2b23d4dff8 and 3914480936 (rebuild_subcontracting_transfers):
    /// - Identifies supplier-warehouse reservations created by the cancelled transfer (<c>FromVoucherType == "StockEntry" &amp;&amp; FromVoucherId == cancelledStockEntryId</c>)
    ///   so they are cancelled alongside it.
    /// - Replays the remaining active transfers against the Subcontracting Order's own reservations (matching serial/batch entries
    ///   when <c>ReservationBasedOn == "Serial and Batch"</c>, or taking <c>Min(ReservedQty, remainingTransferQty)</c> for qty-based reservations)
    ///   so cancelling a transfer only reopens the reservation that transfer actually used.
    /// </summary>
    public static (List<Guid> TransferReservationsToCancel, Dictionary<Guid, decimal> RebuiltTransferredQtyBySreId)
        RebuildSubcontractingTransferReservationsOnCancel(
            Guid cancelledStockEntryId,
            IReadOnlyList<(Guid SreId, Guid ScoSuppliedItemId, decimal ReservedQty, bool IsSerialAndBatch, string? SerialNo, Guid? BatchId, string? FromVoucherType, Guid? FromVoucherId)> allOrderSres,
            IReadOnlyList<(Guid ScoSuppliedItemId, decimal TransferQty, IReadOnlyCollection<string>? SerialNos, IReadOnlyDictionary<Guid, decimal>? BatchQuantities)> remainingActiveTransfers,
            int precision = 4)
    {
        var toCancel = new List<Guid>();
        var rebuilt = new Dictionary<Guid, decimal>();
        if (allOrderSres == null || allOrderSres.Count == 0)
        {
            return (toCancel, rebuilt);
        }

        foreach (var sre in allOrderSres)
        {
            if (sre.FromVoucherId == cancelledStockEntryId &&
                (string.Equals(sre.FromVoucherType, "StockEntry", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(sre.FromVoucherType, "Stock Entry", StringComparison.OrdinalIgnoreCase)))
            {
                toCancel.Add(sre.SreId);
            }
        }

        var transfersBySuppliedRow = new Dictionary<Guid, (decimal RemainingQty, HashSet<string> SerialNos, Dictionary<Guid, decimal> BatchQty)>();
        if (remainingActiveTransfers != null)
        {
            foreach (var tr in remainingActiveTransfers)
            {
                if (!transfersBySuppliedRow.TryGetValue(tr.ScoSuppliedItemId, out var agg))
                {
                    agg = (0m, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<Guid, decimal>());
                }

                var newQty = agg.RemainingQty + tr.TransferQty;
                if (tr.SerialNos != null)
                {
                    foreach (var sn in tr.SerialNos)
                    {
                        if (!string.IsNullOrWhiteSpace(sn))
                            agg.SerialNos.Add(sn.Trim());
                    }
                }
                if (tr.BatchQuantities != null)
                {
                    foreach (var (bId, bQty) in tr.BatchQuantities)
                    {
                        agg.BatchQty[bId] = agg.BatchQty.GetValueOrDefault(bId, 0m) + Math.Abs(bQty);
                    }
                }

                transfersBySuppliedRow[tr.ScoSuppliedItemId] = (newQty, agg.SerialNos, agg.BatchQty);
            }
        }

        foreach (var sre in allOrderSres)
        {
            if (!string.IsNullOrWhiteSpace(sre.FromVoucherType))
            {
                continue;
            }

            if (!transfersBySuppliedRow.TryGetValue(sre.ScoSuppliedItemId, out var transferState))
            {
                rebuilt[sre.SreId] = 0m;
                continue;
            }

            decimal matchedQty;
            if (sre.IsSerialAndBatch)
            {
                if (!string.IsNullOrWhiteSpace(sre.SerialNo))
                {
                    matchedQty = transferState.SerialNos.Remove(sre.SerialNo.Trim()) ? 1m : 0m;
                }
                else if (sre.BatchId.HasValue && transferState.BatchQty.TryGetValue(sre.BatchId.Value, out var availBatchQty))
                {
                    matchedQty = Math.Min(sre.ReservedQty, availBatchQty);
                    transferState.BatchQty[sre.BatchId.Value] = availBatchQty - matchedQty;
                }
                else
                {
                    matchedQty = 0m;
                }
            }
            else
            {
                matchedQty = Math.Min(sre.ReservedQty, transferState.RemainingQty);
            }

            var nextRemaining = Math.Max(0m, transferState.RemainingQty - matchedQty);
            transfersBySuppliedRow[sre.ScoSuppliedItemId] = (nextRemaining, transferState.SerialNos, transferState.BatchQty);
            rebuilt[sre.SreId] = Math.Round(matchedQty, precision, MidpointRounding.AwayFromZero);
        }

        return (toCancel, rebuilt);
    }

    /// <summary>
    /// Applies a "Send to Subcontractor" transfer row against a Subcontracting Order row's Stock Reservation Entries
    /// per ERPNext commits cd897009f7, a56e775582, 189b35ae0a, and b99774a83d:
    /// - Excludes reservations in the target supplier warehouse (<paramref name="targetSupplierWarehouseId"/>) so transfers
    ///   from any source warehouse consume the order's own reservations rather than supplier-warehouse reservations.
    /// - Shares the transferred serial numbers and batch quantities across all matching reservations of the row so no
    ///   serial or batch quantity is counted twice across multiple SREs.
    /// - Rounds each updated <c>TransferredQty</c> to <paramref name="precision"/>.
    /// </summary>
    public static Dictionary<Guid, decimal> ApplySubcontractingTransferToOrderReservations(
        Guid targetSupplierWarehouseId,
        decimal transferStockQty,
        IReadOnlyCollection<string>? transferredSerialNos,
        IReadOnlyDictionary<Guid, decimal>? transferredBatchQuantities,
        IReadOnlyList<(Guid SreId, Guid WarehouseId, decimal ReservedQty, decimal CurrentTransferredQty, bool IsSerialAndBatch, string? SerialNo, Guid? BatchId)> candidateSres,
        int precision = 4)
    {
        var updatedTransferredBySre = new Dictionary<Guid, decimal>();
        if (candidateSres == null || candidateSres.Count == 0 || transferStockQty <= 0m)
        {
            return updatedTransferredBySre;
        }

        var remainingSerials = transferredSerialNos != null
            ? new HashSet<string>(transferredSerialNos.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var remainingBatches = transferredBatchQuantities != null
            ? transferredBatchQuantities.ToDictionary(kv => kv.Key, kv => Math.Abs(kv.Value))
            : new Dictionary<Guid, decimal>();

        var remainingQty = transferStockQty;
        foreach (var sre in candidateSres)
        {
            if (sre.WarehouseId == targetSupplierWarehouseId)
            {
                continue;
            }

            if (Math.Round(remainingQty, precision, MidpointRounding.AwayFromZero) <= 0m)
            {
                break;
            }

            var openOnSre = Math.Max(0m, sre.ReservedQty - sre.CurrentTransferredQty);
            if (openOnSre <= 0m)
            {
                continue;
            }

            decimal workingQty = 0m;
            if (sre.IsSerialAndBatch)
            {
                if (!string.IsNullOrWhiteSpace(sre.SerialNo))
                {
                    if (remainingSerials.Remove(sre.SerialNo.Trim()))
                    {
                        workingQty = Math.Min(1m, openOnSre);
                    }
                }
                else if (sre.BatchId.HasValue && remainingBatches.TryGetValue(sre.BatchId.Value, out var batchAvail) && batchAvail > 0m)
                {
                    workingQty = Math.Min(openOnSre, batchAvail);
                    remainingBatches[sre.BatchId.Value] = batchAvail - workingQty;
                }
            }
            else
            {
                workingQty = Math.Min(openOnSre, remainingQty);
            }

            if (workingQty > 0m)
            {
                updatedTransferredBySre[sre.SreId] = Math.Round(sre.CurrentTransferredQty + workingQty, precision, MidpointRounding.AwayFromZero);
                remainingQty -= workingQty;
            }
        }

        return updatedTransferredBySre;
    }

    /// <summary>
    /// Distributes Subcontracting Receipt / Order additional costs across Finished Good rows using running-total rounding
    /// and guards against zero total weight per ERPNext commits 4884d5e433, 9d9cfcae0c, and c3b83d3d20
    /// (<c>calculate_additional_costs</c> in <c>subcontracting_controller.py</c>):
    /// - Excludes secondary item rows (<c>IsSecondaryItem == true</c>).
    /// - Throws when <paramref name="totalAdditionalCosts"/> != 0 and total FG weight (<c>Amount</c> or <c>QtyForCosting</c>) is 0.
    /// - Rounds cumulative share at each FG row so shares sum to <paramref name="totalAdditionalCosts"/> and no row share goes negative.
    /// </summary>
    public static List<(Guid RowId, decimal ShareAmount, decimal AdditionalCostPerQty)> CalculateAdditionalCostPerQtyForFinishedGoods(
        decimal totalAdditionalCosts,
        string distributeBasedOn,
        IReadOnlyList<(Guid RowId, bool IsSecondaryItem, decimal Amount, decimal QtyForCosting)> rows,
        int precision = 2)
    {
        var result = new List<(Guid RowId, decimal ShareAmount, decimal AdditionalCostPerQty)>();
        if (rows == null || rows.Count == 0)
        {
            return result;
        }

        var fgRows = rows.Where(r => !r.IsSecondaryItem).ToList();
        if (totalAdditionalCosts == 0m)
        {
            foreach (var r in fgRows)
            {
                result.Add((r.RowId, 0m, 0m));
            }
            return result;
        }

        var basedOnAmount = string.Equals(distributeBasedOn, "Amount", StringComparison.OrdinalIgnoreCase);
        var weights = fgRows.Select(r => basedOnAmount ? r.Amount : r.QtyForCosting).ToList();
        var totalWeight = weights.Sum();

        if (totalWeight == 0m)
        {
            var basisLabel = basedOnAmount ? "Amount" : "Qty";
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot distribute Additional Costs based on {basisLabel}, as the finished goods have zero {basisLabel}.");
        }

        decimal allocated = 0m;
        decimal cumulativeWeight = 0m;

        for (var i = 0; i < fgRows.Count; i++)
        {
            var fg = fgRows[i];
            cumulativeWeight += weights[i];
            var cumulativeShare = Math.Round(
                totalAdditionalCosts * cumulativeWeight / totalWeight,
                precision,
                MidpointRounding.AwayFromZero);
            var share = Math.Round(cumulativeShare - allocated, precision, MidpointRounding.AwayFromZero);
            allocated = cumulativeShare;

            var perQty = fg.QtyForCosting != 0m ? share / fg.QtyForCosting : 0m;
            result.Add((fg.RowId, share, perQty));
        }

        return result;
    }

    /// <summary>
    /// Costs a Subcontracting Order's supplied raw materials and <c>rm_cost_per_qty</c> in company currency
    /// from the BOM Item's unrounded <c>BaseRate</c> per ERPNext commits adbe37cbad, 922ad20647, and eb6aecb851.
    /// </summary>
    public static (decimal SuppliedItemRate, decimal SuppliedItemAmount, decimal OrderItemRmCostPerQty) CalculateSubcontractingRawMaterialCostInCompanyCurrency(
        decimal bomItemBaseRate,
        decimal bomItemConversionFactor,
        decimal suppliedRequiredQty,
        decimal bomQuantity,
        IEnumerable<(decimal BaseRate, decimal Qty)>? allBomItems = null,
        int ratePrecision = 2,
        int amountPrecision = 2,
        int rmCostPerQtyPrecision = 2)
    {
        var factor = bomItemConversionFactor > 0m ? bomItemConversionFactor : 1m;
        var unroundedRate = bomItemBaseRate / factor;
        var amount = Math.Round(suppliedRequiredQty * unroundedRate, amountPrecision, MidpointRounding.AwayFromZero);
        var rate = Math.Round(unroundedRate, ratePrecision, MidpointRounding.AwayFromZero);

        var totalBomRmBaseCost = allBomItems != null
            ? allBomItems.Sum(i => i.BaseRate * i.Qty)
            : (bomItemBaseRate * suppliedRequiredQty);
        var bomQty = bomQuantity > 0m ? bomQuantity : 1m;
        var rmCostPerQty = Math.Round(totalBomRmBaseCost / bomQty, rmCostPerQtyPrecision, MidpointRounding.AwayFromZero);

        return (rate, amount, rmCostPerQty);
    }

    /// <summary>
    /// Validates that every Subcontracting Order Finished Good row has a matching Service Item row paired by
    /// <c>PurchaseOrderItemId</c> per ERPNext commits cd45f385e5 and 90944b84dd (<c>subcontracting_order.py</c>).
    /// </summary>
    public static void ValidateSubcontractingOrderServiceItemPairing(
        IEnumerable<(int RowNumber, string ItemCode, Guid? PurchaseOrderItemId)> finishedGoodRows,
        IEnumerable<Guid?> serviceItemPurchaseOrderItemIds)
    {
        if (finishedGoodRows == null)
        {
            return;
        }

        var servicePoItems = new HashSet<Guid>(
            (serviceItemPurchaseOrderItemIds ?? Enumerable.Empty<Guid?>())
                .Where(id => id.HasValue && id.Value != Guid.Empty)
                .Select(id => id!.Value));

        foreach (var fg in finishedGoodRows)
        {
            if (!fg.PurchaseOrderItemId.HasValue || !servicePoItems.Contains(fg.PurchaseOrderItemId.Value))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row {fg.RowNumber}: Finished Good {fg.ItemCode} has no Service Item row.");
            }
        }
    }

    /// <summary>
    /// Validates on a Subcontracting Return against a Subcontracting Inward Order that no row returns more of a batch
    /// than was delivered and not yet returned (tracking remaining open batch qty across rows in the entry)
    /// per ERPNext commits 9b5a83412c and 7e4719bac3 (<c>validate_returned_batch_qty</c> in <c>subcontracting_inward_controller.py</c>).
    /// </summary>
    public static void ValidateInwardReturnBatchQuantities(
        string scioNumber,
        IReadOnlyDictionary<(Guid ScioDetailId, string BatchNo), decimal> unreturnedDeliveredBatchQtyByDetail,
        IEnumerable<(int RowNumber, Guid ScioDetailId, IReadOnlyDictionary<string, decimal> BatchQuantities)> returnRows,
        int precision = 4)
    {
        if (returnRows == null)
        {
            return;
        }

        var openQty = new Dictionary<(Guid ScioDetailId, string BatchNo), decimal>();
        if (unreturnedDeliveredBatchQtyByDetail != null)
        {
            foreach (var (k, v) in unreturnedDeliveredBatchQtyByDetail)
            {
                openQty[k] = v;
            }
        }

        foreach (var row in returnRows)
        {
            if (row.BatchQuantities == null)
            {
                continue;
            }

            foreach (var (batchNo, rawQty) in row.BatchQuantities)
            {
                var qty = Math.Abs(rawQty);
                var key = (row.ScioDetailId, batchNo);
                var allowedQty = Math.Round(openQty.GetValueOrDefault(key, 0m), precision, MidpointRounding.AwayFromZero);
                openQty[key] = allowedQty - qty;

                if (Math.Round(qty, precision, MidpointRounding.AwayFromZero) > allowedQty)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{row.RowNumber}: Only {allowedQty} of Batch {batchNo} delivered against Subcontracting Inward Order {scioNumber} is left to return.");
                }
            }
        }
    }

    /// <summary>
    /// Aggregates secondary item rows on a Subcontracting Inward Manufacture Stock Entry by <c>(ItemId, TargetWarehouseId)</c>,
    /// rounds the summed quantity to <paramref name="producedQtyPrecision"/>, validates on cancel that all rows of a secondary item
    /// together do not reduce <c>ProducedQty</c> below <c>DeliveredQty</c>, and produces one deduplicated row per
    /// <c>(ItemId, TargetWarehouseId)</c> per ERPNext commits 3770398f2f, 19b846a497, and a7755dcb8d.
    /// </summary>
    public static Dictionary<(Guid ItemId, Guid TargetWarehouseId), decimal> AggregateAndValidateInwardSecondaryItems(
        bool isCancel,
        IEnumerable<(Guid ItemId, string ItemCode, Guid TargetWarehouseId, decimal TransferQty)> entrySecondaryRows,
        IReadOnlyDictionary<(Guid ItemId, Guid TargetWarehouseId), (decimal ProducedQty, decimal DeliveredQty)>? existingScioSecondaryItems = null,
        int producedQtyPrecision = 4)
    {
        var summed = new Dictionary<(Guid ItemId, Guid TargetWarehouseId), decimal>();
        var itemCodes = new Dictionary<(Guid ItemId, Guid TargetWarehouseId), string>();

        if (entrySecondaryRows == null)
        {
            return summed;
        }

        foreach (var row in entrySecondaryRows)
        {
            var key = (row.ItemId, row.TargetWarehouseId);
            var delta = isCancel ? -row.TransferQty : row.TransferQty;
            summed[key] = summed.GetValueOrDefault(key, 0m) + delta;
            itemCodes[key] = row.ItemCode;
        }

        foreach (var key in summed.Keys.ToList())
        {
            summed[key] = Math.Round(summed[key], producedQtyPrecision, MidpointRounding.AwayFromZero);

            if (isCancel &&
                existingScioSecondaryItems != null &&
                existingScioSecondaryItems.TryGetValue(key, out var existing))
            {
                var remainingProduced = Math.Round(
                    existing.ProducedQty - Math.Abs(summed[key]),
                    producedQtyPrecision,
                    MidpointRounding.AwayFromZero);
                var delivered = Math.Round(existing.DeliveredQty, producedQtyPrecision, MidpointRounding.AwayFromZero);
                if (delivered > remainingProduced)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Cannot cancel Manufacture Stock Entry because Secondary Item {itemCodes[key]} has already delivered {existing.DeliveredQty}.");
                }
            }
        }

        return summed;
    }

    /// <summary>
    /// Validates Subcontracting Order finished good rows against the linked Purchase Order's pending subcontracted
    /// quantities using a pre-fetched map of pending qty per <c>PurchaseOrderItemId</c> (deducting across multiple
    /// SCO rows that reference the same PO row) per ERPNext commits 953cb1a40f, 04b814303a, and 0a134f3d98.
    /// </summary>
    public static void ValidateSubcontractingOrderItemsWithCachedPendingQty(
        IReadOnlyDictionary<Guid, decimal> pendingSubcontractableQtyByPoItemId,
        IEnumerable<(int RowNumber, Guid? PurchaseOrderItemId, decimal ScoStockQty)> scoItems,
        int precision = 4)
    {
        if (pendingSubcontractableQtyByPoItemId == null || scoItems == null)
        {
            return;
        }

        var remainingByPoItem = new Dictionary<Guid, decimal>(pendingSubcontractableQtyByPoItemId);
        foreach (var row in scoItems)
        {
            if (row.ScoStockQty <= 0m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowNumber}: Quantity must be greater than 0.");
            }

            if (!row.PurchaseOrderItemId.HasValue || row.PurchaseOrderItemId.Value == Guid.Empty)
            {
                continue;
            }

            var poItemId = row.PurchaseOrderItemId.Value;
            var available = Math.Round(remainingByPoItem.GetValueOrDefault(poItemId, 0m), precision, MidpointRounding.AwayFromZero);
            var requested = Math.Round(row.ScoStockQty, precision, MidpointRounding.AwayFromZero);
            if (requested > available)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowNumber}: Subcontracted quantity {requested} cannot exceed pending Purchase Order item quantity {available}.");
            }

            remainingByPoItem[poItemId] = available - requested;
        }
    }

    /// <summary>
    /// Removes only the service item row paired with the deleted finished good row's <paramref name="deletedPurchaseOrderItemId"/>
    /// (falling back to <paramref name="deletedFgItemId"/> only when <c>PurchaseOrderItemId</c> is unset), so sibling
    /// rows for the same finished good item on other PO rows keep their service items
    /// per ERPNext commit 4211b7d593 (<c>subcontracting_order.js</c>).
    /// </summary>
    public static List<(Guid ServiceRowId, Guid? PurchaseOrderItemId, Guid FgItemId)> RemovePairedServiceItemByPurchaseOrderItem(
        Guid? deletedPurchaseOrderItemId,
        Guid deletedFgItemId,
        IEnumerable<(Guid ServiceRowId, Guid? PurchaseOrderItemId, Guid FgItemId)> serviceItems)
    {
        if (serviceItems == null)
        {
            return new List<(Guid ServiceRowId, Guid? PurchaseOrderItemId, Guid FgItemId)>();
        }

        if (deletedPurchaseOrderItemId.HasValue && deletedPurchaseOrderItemId.Value != Guid.Empty)
        {
            return serviceItems
                .Where(s => s.PurchaseOrderItemId != deletedPurchaseOrderItemId)
                .ToList();
        }

        return serviceItems
            .Where(s => s.FgItemId != deletedFgItemId)
            .ToList();
    }

    /// <summary>
    /// Prepares Subcontract Order Summary report rows so finished good items whose raw materials are all sourced
    /// by the job worker (having no <c>supplied_items</c> rows) are still included in the report output
    /// per ERPNext commit dd22b835f9 (<c>subcontract_order_summary.py</c>).
    /// </summary>
    public static List<(Guid ScoItemId, Guid FgItemId, decimal OrderedQty, decimal ReceivedQty, Guid? SuppliedRmItemId, decimal? RequiredRmQty, decimal? SuppliedRmQty)> PrepareSubcontractOrderSummaryRows(
        IEnumerable<(Guid ScoItemId, Guid FgItemId, decimal OrderedQty, decimal ReceivedQty)> scoItems,
        IEnumerable<(Guid MainItemRowId, Guid RmItemId, decimal RequiredQty, decimal SuppliedQty)>? suppliedItems)
    {
        var result = new List<(Guid ScoItemId, Guid FgItemId, decimal OrderedQty, decimal ReceivedQty, Guid? SuppliedRmItemId, decimal? RequiredRmQty, decimal? SuppliedRmQty)>();
        if (scoItems == null)
        {
            return result;
        }

        var suppliedByRow = (suppliedItems ?? Enumerable.Empty<(Guid MainItemRowId, Guid RmItemId, decimal RequiredQty, decimal SuppliedQty)>())
            .GroupBy(s => s.MainItemRowId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var item in scoItems)
        {
            if (suppliedByRow.TryGetValue(item.ScoItemId, out var rmRows) && rmRows.Count > 0)
            {
                foreach (var rm in rmRows)
                {
                    result.Add((item.ScoItemId, item.FgItemId, item.OrderedQty, item.ReceivedQty, rm.RmItemId, rm.RequiredQty, rm.SuppliedQty));
                }
            }
            else
            {
                result.Add((item.ScoItemId, item.FgItemId, item.OrderedQty, item.ReceivedQty, null, null, null));
            }
        }

        return result;
    }

    /// <summary>
    /// Computes net raw material sent to the supplier in stock UOM (<c>TransferQty</c>/<c>StockQty</c>),
    /// negating return Material Transfer entries, and normalizes generated Stock Entry transfer/return rows
    /// to stock UOM with <c>ConversionFactor = 1</c> per ERPNext PR #67367 (commit ca527fd48c).
    /// </summary>
    public static Dictionary<Guid, (decimal StockQty, string StockUom, decimal ConversionFactor)> CalculateMaterialSentToSupplierInStockUom(
        IEnumerable<(Guid ItemId, string StockUom, decimal TransferStockQty, bool IsMaterialTransferReturn)> stockEntryDetails,
        int precision = 4)
    {
        var totals = new Dictionary<Guid, (decimal StockQty, string StockUom, decimal ConversionFactor)>();
        if (stockEntryDetails == null)
        {
            return totals;
        }

        foreach (var row in stockEntryDetails)
        {
            var signedQty = row.IsMaterialTransferReturn ? -Math.Abs(row.TransferStockQty) : row.TransferStockQty;
            if (totals.TryGetValue(row.ItemId, out var existing))
            {
                totals[row.ItemId] = (
                    Math.Round(existing.StockQty + signedQty, precision, MidpointRounding.AwayFromZero),
                    string.IsNullOrWhiteSpace(existing.StockUom) ? row.StockUom : existing.StockUom,
                    1m);
            }
            else
            {
                totals[row.ItemId] = (
                    Math.Round(signedQty, precision, MidpointRounding.AwayFromZero),
                    row.StockUom,
                    1m);
            }
        }

        return totals;
    }

    /// <summary>
    /// Validates that every Additional Costs expense account on a Subcontracting Order or non-return
    /// Subcontracting Receipt is denominated in the company currency per ERPNext PR #67403 (commit 168c8ede47).
    /// Skipped on return Subcontracting Receipts and on Subcontracting Inward Orders (commits 2fb3f2c1d1, 60295657d4).
    /// </summary>
    public static void ValidateAdditionalCostAccountCurrencies(
        string companyCurrency,
        IEnumerable<(int RowNumber, string AccountName, string? AccountCurrency)>? additionalCosts,
        bool isReturn = false,
        bool isInwardOrder = false)
    {
        if (isReturn || isInwardOrder || additionalCosts == null || string.IsNullOrWhiteSpace(companyCurrency))
        {
            return;
        }

        var expectedCurrency = companyCurrency.Trim();
        foreach (var row in additionalCosts)
        {
            if (string.IsNullOrWhiteSpace(row.AccountName) || string.IsNullOrWhiteSpace(row.AccountCurrency))
            {
                continue;
            }

            if (!string.Equals(row.AccountCurrency.Trim(), expectedCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("row", row.RowNumber)
                    .WithData("account", row.AccountName)
                    .WithData("companyCurrency", expectedCurrency)
                    .WithData("detail", $"Row #{row.RowNumber}: Additional Costs account {row.AccountName} is not in the company currency {expectedCurrency}. Select an account in {expectedCurrency}.");
            }
        }
    }

    /// <summary>
    /// Determines whether a non-return Subcontracting Receipt still has unreturned rejected quantity
    /// after accounting for submitted rejected-warehouse returns per ERPNext PR #67404 (commit ef84f3c6bd).
    /// </summary>
    public static bool HasRejectedQtyToReturn(
        SubcontractingReceipt receipt,
        IEnumerable<SubcontractingReceipt>? submittedReturns,
        int precision = 4)
    {
        if (receipt == null || receipt.IsReturn)
        {
            return false;
        }

        var rejectedRows = receipt.Items
            .Where(i => !IsSecondaryItem(i) && Math.Round(i.RejectedQty, precision, MidpointRounding.AwayFromZero) > 0m)
            .ToList();
        if (rejectedRows.Count == 0)
        {
            return false;
        }

        var returnedRejectedByItem = (submittedReturns ?? Enumerable.Empty<SubcontractingReceipt>())
            .Where(r => r.IsReturn
                        && r.ReturnAgainstReceiptId == receipt.Id
                        && r.Status == SubcontractingReceiptStatus.Submitted)
            .SelectMany(r => r.Items)
            .Where(i => !IsSecondaryItem(i) && IsRejectedSubcontractingReturn(
                i.ReturnQtyFromRejectedWarehouse, i.WarehouseId, i.RejectedWarehouseId))
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Qty)));

        foreach (var row in rejectedRows)
        {
            var returned = returnedRejectedByItem.GetValueOrDefault(row.ItemId, 0m);
            if (Math.Round(row.RejectedQty - returned, precision, MidpointRounding.AwayFromZero) > 0m)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Filters rejected-warehouse return candidate rows to drop items whose rejected quantity is already
    /// fully returned, throwing when no returnable rejected rows remain per ERPNext PR #67404 (commit ef84f3c6bd).
    /// </summary>
    public static List<(Guid ItemId, decimal RemainingRejectedQty)> FilterReturnableRejectedRows(
        string docType,
        string documentNumber,
        IEnumerable<(Guid ItemId, decimal RejectedQty, decimal PriorReturnedRejectedQty)> candidateRows,
        int precision = 4)
    {
        var remaining = new List<(Guid ItemId, decimal RemainingRejectedQty)>();
        if (candidateRows != null)
        {
            foreach (var row in candidateRows)
            {
                var left = Math.Round(row.RejectedQty - Math.Abs(row.PriorReturnedRejectedQty), precision, MidpointRounding.AwayFromZero);
                if (left > 0m)
                {
                    remaining.Add((row.ItemId, left));
                }
            }
        }

        if (remaining.Count == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("docType", docType)
                .WithData("documentNumber", documentNumber)
                .WithData("detail", $"All rejected qty of {docType} {documentNumber} is already returned.");
        }

        return remaining;
    }

    /// <summary>
    /// Partitions Subcontracting Order raw material reservation requests into Production Plan handoff items
    /// (where the Production Plan holds remaining reserved stock for the material) and direct stock reservation
    /// items (where the Production Plan has no reservation row or no remaining reserved qty)
    /// per ERPNext PR #67358 (commit 1e38bbed52).
    /// </summary>
    public static (List<SubcontractingReservationPlanHandoff> PlanHandoffItems, List<SubcontractingDirectReservationItem> DirectStockItems)
        PartitionSubcontractingReservationsForProductionPlan(
            Guid? productionPlanId,
            IEnumerable<(Guid SuppliedItemId, Guid RmItemId, Guid? ReserveWarehouseId, Guid? ProductionPlanSubAssemblyItemId, decimal RequiredQty, decimal StockReservedQty)> suppliedItems,
            IReadOnlyDictionary<(Guid SubAssemblyItemId, Guid RmItemId), (Guid PlanMrItemId, decimal PlanStockReservedQty)>? planReservationRowsByKey,
            int precision = 4)
    {
        var planItems = new List<SubcontractingReservationPlanHandoff>();
        var directItems = new List<SubcontractingDirectReservationItem>();

        if (suppliedItems == null)
        {
            return (planItems, directItems);
        }

        foreach (var item in suppliedItems)
        {
            if (productionPlanId.HasValue
                && productionPlanId.Value != Guid.Empty
                && item.ProductionPlanSubAssemblyItemId.HasValue
                && planReservationRowsByKey != null
                && planReservationRowsByKey.TryGetValue((item.ProductionPlanSubAssemblyItemId.Value, item.RmItemId), out var planRow)
                && Math.Round(item.StockReservedQty, precision, MidpointRounding.AwayFromZero)
                    < Math.Round(planRow.PlanStockReservedQty, precision, MidpointRounding.AwayFromZero))
            {
                planItems.Add(new SubcontractingReservationPlanHandoff(
                    item.SuppliedItemId,
                    item.RmItemId,
                    item.ReserveWarehouseId,
                    item.RequiredQty,
                    productionPlanId.Value,
                    planRow.PlanMrItemId));
                continue;
            }

            directItems.Add(new SubcontractingDirectReservationItem(
                item.SuppliedItemId,
                item.RmItemId,
                item.ReserveWarehouseId,
                item.RequiredQty));
        }

        return (planItems, directItems);
    }

    /// <summary>
    /// Computes Purchase Receipt service item quantities from linked Subcontracting Receipt finished-good rows,
    /// including <c>ProcessLossQty</c> in the billable/receivable service quantity because the job worker
    /// performed the service on process-loss units too per ERPNext PR #67405 (commit 5e3db1d356).
    /// </summary>
    public static (decimal ServiceQty, decimal RejectedServiceQty) CalculatePurchaseReceiptServiceQuantitiesFromSubcontractingReceipt(
        decimal serviceToFgRatio,
        IEnumerable<(decimal AcceptedQty, decimal ProcessLossQty, decimal RejectedQty)> scrFinishedGoodItems,
        int precision = 4)
    {
        if (scrFinishedGoodItems == null)
        {
            return (0m, 0m);
        }

        decimal totalWorkedFgQty = 0m;
        decimal totalRejectedFgQty = 0m;
        foreach (var item in scrFinishedGoodItems)
        {
            totalWorkedFgQty += item.AcceptedQty + item.ProcessLossQty;
            totalRejectedFgQty += item.RejectedQty;
        }

        return (
            Math.Round(serviceToFgRatio * totalWorkedFgQty, precision, MidpointRounding.AwayFromZero),
            Math.Round(serviceToFgRatio * totalRejectedFgQty, precision, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Replays supplier transfer and return stock entries in chronological order
    /// (<c>PostingDateTime, CreationTime</c>) to determine the serial numbers and batch quantities
    /// currently held at the supplier per ERPNext PR #67370 (commit 72819fa0e3):
    /// - Transfers append serial numbers and add batch quantities.
    /// - Return transfers remove returned serial numbers and subtract returned batch quantities.
    /// </summary>
    public static (List<string> AvailableSerialNos, Dictionary<Guid, decimal> AvailableBatchQuantities)
        BuildSupplierMaterialLotsChronological(
            IEnumerable<(DateTime PostingDateTime, DateTime CreationTime, bool IsReturn, IEnumerable<string>? SerialNos, IReadOnlyDictionary<Guid, decimal>? BatchQuantities)> movements)
    {
        var serialNos = new List<string>();
        var batchQuantities = new Dictionary<Guid, decimal>();

        if (movements == null)
        {
            return (serialNos, batchQuantities);
        }

        foreach (var mv in movements.OrderBy(m => m.PostingDateTime).ThenBy(m => m.CreationTime))
        {
            if (mv.SerialNos != null)
            {
                foreach (var rawSn in mv.SerialNos)
                {
                    if (string.IsNullOrWhiteSpace(rawSn)) continue;
                    var sn = rawSn.Trim();
                    if (!mv.IsReturn)
                    {
                        serialNos.Add(sn);
                    }
                    else
                    {
                        var idx = serialNos.FindIndex(s => string.Equals(s, sn, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                            serialNos.RemoveAt(idx);
                    }
                }
            }

            if (mv.BatchQuantities != null)
            {
                foreach (var (batchId, rawQty) in mv.BatchQuantities)
                {
                    var absQty = Math.Abs(rawQty);
                    if (absQty <= 0m) continue;
                    var delta = mv.IsReturn ? -absQty : absQty;
                    batchQuantities[batchId] = batchQuantities.GetValueOrDefault(batchId, 0m) + delta;
                }
            }
        }

        return (serialNos, batchQuantities);
    }

    /// <summary>
    /// Validates that every serial number consumed on a Subcontracting Receipt is currently present
    /// in the supplier's available serial pool (rejecting even when the pool is empty, e.g. after all
    /// supplied serials were returned) per ERPNext PR #67370 (commit 72819fa0e3, <c>__validate_serial_no</c>).
    /// </summary>
    public static void ValidateConsumedSerialNosAgainstSupplierPool(
        string orderDocType,
        string orderNumber,
        IEnumerable<string>? availableSerialNosAtSupplier,
        IEnumerable<string>? consumedSerialNos)
    {
        if (consumedSerialNos == null) return;

        var available = new HashSet<string>(
            (availableSerialNosAtSupplier ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var incorrect = consumedSerialNos
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => !available.Contains(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (incorrect.Count > 0)
        {
            var joined = string.Join("\n", incorrect);
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("orderNumber", orderNumber)
                .WithData("incorrectSerialNos", joined)
                .WithData("detail", $"The Serial Nos {joined} have not been supplied against the {orderDocType} {orderNumber}");
        }
    }

    /// <summary>
    /// Allocates consumed raw material (and alternative item) quantities onto Subcontracting Order
    /// supplied item rows based on what items were actually sent on each row, ignoring negative net
    /// sent quantities caused by component returns booked on one row per ERPNext PR #67373 (commit 173f26d7ce).
    /// </summary>
    public static Dictionary<Guid, decimal> AllocateConsumedQtyOnOrderRows(
        IEnumerable<(Guid OrderRowId, Guid MainItemId, Guid OrderId)> orderSuppliedRowsInIdxOrder,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, decimal>> netItemsSentByOrderRow,
        IReadOnlyDictionary<(Guid ItemId, Guid MainItemId, Guid OrderId), decimal> itemwiseConsumedQty,
        int precision = 4)
    {
        var result = new Dictionary<Guid, decimal>();
        if (orderSuppliedRowsInIdxOrder == null)
            return result;

        var remainingConsumed = itemwiseConsumedQty != null
            ? new Dictionary<(Guid ItemId, Guid MainItemId, Guid OrderId), decimal>(itemwiseConsumedQty)
            : new Dictionary<(Guid ItemId, Guid MainItemId, Guid OrderId), decimal>();

        foreach (var row in orderSuppliedRowsInIdxOrder)
        {
            decimal consumedOnRow = 0m;
            if (netItemsSentByOrderRow != null &&
                netItemsSentByOrderRow.TryGetValue(row.OrderRowId, out var sentByItem) &&
                sentByItem != null)
            {
                foreach (var (sentItemId, sentQty) in sentByItem)
                {
                    var key = (sentItemId, row.MainItemId, row.OrderId);
                    var availableConsumed = remainingConsumed.GetValueOrDefault(key, 0m);
                    var positiveSent = Math.Max(sentQty, 0m);
                    var allocated = Math.Min(availableConsumed, positiveSent);
                    consumedOnRow += allocated;
                    remainingConsumed[key] = availableConsumed - allocated;
                }
            }

            result[row.OrderRowId] = Math.Round(consumedOnRow, precision, MidpointRounding.AwayFromZero);
        }

        return result;
    }

    /// <summary>
    /// Distributes Subcontracting Order additional costs across finished-good rows using each row's
    /// pre-additional-cost amount (<c>Qty * (RmCostPerQty + ServiceCostPerQty)</c>) when distributing by Amount.
    /// Per ERPNext PR #67361 (commit 4fc3e35748): prevents repeated saves from compounding previous
    /// additional costs into the distribution basis.
    /// </summary>
    public static IReadOnlyList<(Guid RowId, decimal AdditionalCostPerQty, decimal FinalRate, decimal FinalAmount)> DistributeOrderAdditionalCostsByPreCostAmount(
        IReadOnlyList<(Guid RowId, decimal Qty, decimal RmCostPerQty, decimal ServiceCostPerQty)> rows,
        decimal totalAdditionalCost,
        bool distributeByQty = false,
        int precision = 4)
    {
        if (rows == null || rows.Count == 0)
        {
            return Array.Empty<(Guid, decimal, decimal, decimal)>();
        }

        var preAmounts = rows.Select(r => r.Qty * (r.RmCostPerQty + r.ServiceCostPerQty)).ToList();
        var totalBasis = distributeByQty ? rows.Sum(r => r.Qty) : preAmounts.Sum();

        var result = new List<(Guid, decimal, decimal, decimal)>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var basis = distributeByQty ? row.Qty : preAmounts[i];
            var allocatedCost = totalBasis != 0m && totalAdditionalCost != 0m
                ? totalAdditionalCost * (basis / totalBasis)
                : 0m;
            var additionalCostPerQty = row.Qty != 0m
                ? Math.Round(allocatedCost / row.Qty, precision, MidpointRounding.AwayFromZero)
                : 0m;
            var finalRate = Math.Round(row.RmCostPerQty + row.ServiceCostPerQty + additionalCostPerQty, precision, MidpointRounding.AwayFromZero);
            var finalAmount = Math.Round(row.Qty * finalRate, precision, MidpointRounding.AwayFromZero);

            result.Add((row.RowId, additionalCostPerQty, finalRate, finalAmount));
        }

        return result;
    }

    /// <summary>
    /// Computes mapped process loss quantity on a Subcontracting Receipt row created from a Subcontracting Order
    /// per ERPNext PR #67461 (commits 94dd20a724, 32ffe97c4d: <c>get_mapped_process_loss_qty</c>):
    /// - For a whole-number UOM, rounds running cumulative loss on <c>receivedBefore + rowQty</c> minus
    ///   running cumulative loss on <c>receivedBefore</c> so partial receipts together lose the BOM's exact process loss in whole units.
    /// - For fractional UOMs, keeps per-receipt rounding at field precision.
    /// </summary>
    public static decimal CalculateMappedProcessLossQty(
        decimal rowQty,
        decimal receivedBefore,
        decimal processLossPercentage,
        bool mustBeWholeNumber,
        int precision = 4)
    {
        if (rowQty <= 0m || processLossPercentage <= 0m)
        {
            return 0m;
        }

        if (!mustBeWholeNumber)
        {
            return Math.Round(rowQty * (processLossPercentage / 100m), precision, MidpointRounding.AwayFromZero);
        }

        decimal WholeLossOn(decimal qty) =>
            Math.Round(qty * (processLossPercentage / 100m), 0, MidpointRounding.AwayFromZero);

        return WholeLossOn(receivedBefore + rowQty) - WholeLossOn(receivedBefore);
    }

    /// <summary>
    /// Computes the subcontracting service cost credited (or debited on return) to the service account in GL
    /// per ERPNext PR #67460 (commits 9136647a14, 724dbf5c6b, 298036aa36 in <c>gl_composer.py</c>):
    /// - On receipt: stock value absorbs the service cost of accepted, rejected, and process-loss units,
    ///   so service cost is <c>serviceCostPerQty * (acceptedQty + rejectedQty + processLossQty)</c>.
    /// - On return of accepted qty: scales returned accepted qty by the original receipt's
    ///   <c>receivedQty / acceptedQty</c> ratio (<paramref name="returnReceivedQtyRatio"/>) so the full service cost reverses.
    /// </summary>
    public static decimal CalculateSubcontractingServiceCostForGl(
        decimal serviceCostPerQty,
        decimal acceptedQty,
        decimal rejectedQty,
        decimal processLossQty,
        bool isReturn = false,
        decimal? returnReceivedQtyRatio = null,
        int precision = 2)
    {
        decimal serviceQty;
        if (isReturn && returnReceivedQtyRatio.HasValue)
        {
            serviceQty = acceptedQty * returnReceivedQtyRatio.Value;
        }
        else
        {
            serviceQty = acceptedQty + rejectedQty + processLossQty;
        }

        return Math.Round(serviceCostPerQty * serviceQty, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes finished-good RM cost per qty FIRST, then prices own-cost secondary items at posting-time valuation,
    /// then prices percentage secondary items from the finished good's current RM + service cost net of own-cost rows
    /// per ERPNext PR #67471 (commits 9eae88272a, 06eb206cf3, 41afbe4e7c).
    /// </summary>
    public static SubcontractingReceiptCostingResult CalculateReceiptFinishedGoodAndSecondaryRates(
        decimal fgAcceptedQty,
        decimal fgReceivedQty,
        decimal suppliedRmTotalAmount,
        decimal serviceCostPerQty,
        decimal additionalCostPerQty,
        decimal lcvAmount,
        IEnumerable<SubcontractingSecondaryRowInput> secondaryRows,
        Func<Guid, Guid?, decimal> resolveValuationRateAtPosting,
        int precision = 4)
    {
        var effectiveReceivedQty = fgReceivedQty != 0m ? fgReceivedQty : fgAcceptedQty;
        var rmCostPerQty = effectiveReceivedQty != 0m
            ? suppliedRmTotalAmount / effectiveReceivedQty
            : 0m;

        var rows = (secondaryRows ?? Array.Empty<SubcontractingSecondaryRowInput>()).ToList();
        var ratedRows = new Dictionary<Guid, (decimal Rate, decimal Amount)>();
        var ownSecondaryCost = 0m;

        // Pass 1: Own-cost rows (Manual / ValuationRate) valued at posting time
        foreach (var row in rows.Where(r => r.ValuationType is Manufacturing.SecondaryItemValuationType.Manual or Manufacturing.SecondaryItemValuationType.ValuationRate))
        {
            var rate = ResolveSecondaryItemRate(
                isReturn: false,
                secondaryQty: row.Qty,
                valuationType: row.ValuationType,
                bomSecondaryBaseCost: row.BomSecondaryBaseCost,
                bomSecondaryStockQty: row.BomSecondaryStockQty,
                resolveWarehouseValuationRate: () => resolveValuationRateAtPosting(row.ItemId, row.WarehouseId),
                precision: precision);
            var amount = Math.Round(row.Qty * rate, precision, MidpointRounding.AwayFromZero);
            ratedRows[row.RowId] = (rate, amount);
            ownSecondaryCost += amount;
        }

        // Pass 2: Percentage rows priced from current RM cost + service cost net of own-cost rows
        var totalSecondaryCost = ownSecondaryCost;
        foreach (var row in rows.Where(r => r.ValuationType == Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost))
        {
            var rate = CalculatePercentageSecondaryRate(
                fgReceivedQty: effectiveReceivedQty,
                rmCostPerQty: rmCostPerQty,
                serviceCostPerQty: serviceCostPerQty,
                additionalCostPerQty: additionalCostPerQty,
                lcvAmount: lcvAmount,
                ownSecondaryCost: ownSecondaryCost,
                costAllocationPercentage: row.CostAllocationPercentage,
                secondaryQty: row.Qty,
                precision: precision);
            var amount = Math.Round(row.Qty * rate, precision, MidpointRounding.AwayFromZero);
            ratedRows[row.RowId] = (rate, amount);
            totalSecondaryCost += amount;
        }

        var secondaryCostPerQty = fgAcceptedQty != 0m
            ? totalSecondaryCost / fgAcceptedQty
            : 0m;
        var fgRate = CalculateFinishedGoodRateWithSecondaryCost(
            serviceCostPerQty + additionalCostPerQty + (effectiveReceivedQty != 0m ? lcvAmount / effectiveReceivedQty : 0m),
            rmCostPerQty,
            secondaryCostPerQty,
            precision);

        return new SubcontractingReceiptCostingResult(
            Math.Round(rmCostPerQty, precision, MidpointRounding.AwayFromZero),
            Math.Round(secondaryCostPerQty, precision, MidpointRounding.AwayFromZero),
            fgRate,
            ratedRows);
    }

    /// <summary>
    /// Computes accepted finished-good received quantities for Production Plan subcontracted sub-assembly rows
    /// from submitted Subcontracting Orders / Receipts (via SCO Item -> PO Item -> ProductionPlanSubAssemblyItem)
    /// per ERPNext PR #67483 (commits 318766f908, 41841dd27b, e672df9bb3: <c>update_subcontracted_received_qty</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateSubcontractedPlanReceivedQuantities(
        IEnumerable<Guid> affectedPlanSubAssemblyItemIds,
        IEnumerable<(Guid PurchaseOrderItemId, Guid ProductionPlanSubAssemblyItemId)> submittedPoItems,
        IReadOnlyDictionary<Guid, decimal> subcontractedReceivedQtyByPoItemId,
        int precision = 4)
    {
        var totals = new Dictionary<Guid, decimal>();
        foreach (var id in affectedPlanSubAssemblyItemIds ?? Array.Empty<Guid>())
        {
            totals[id] = 0m;
        }

        foreach (var (poItemId, planSubAssemblyItemId) in submittedPoItems ?? Array.Empty<(Guid, Guid)>())
        {
            if (!totals.ContainsKey(planSubAssemblyItemId))
                continue;

            var received = subcontractedReceivedQtyByPoItemId.GetValueOrDefault(poItemId, 0m);
            totals[planSubAssemblyItemId] += received;
        }

        foreach (var key in totals.Keys.ToList())
        {
            totals[key] = Math.Round(totals[key], precision, MidpointRounding.AwayFromZero);
        }

        return totals;
    }

    /// <summary>
    /// Computes the pro-rata share of a Subcontracting Order's additional costs that a Subcontracting Receipt takes:
    /// what earlier submitted receipts left of them, in proportion to the qty this receipt takes of what is still pending.
    /// Per ERPNext PR #67357 (<c>get_additional_cost_share</c>).
    /// </summary>
    public static decimal CalculateReceiptAdditionalCostShare(
        IEnumerable<(Guid OrderRowId, decimal OrderedQty, decimal ReceivedQty, decimal AdditionalCostPerQty)> orderItems,
        decimal additionalCostTakenByEarlierReceipts,
        IReadOnlyDictionary<Guid, decimal>? receiptQtyByOrderRowId = null)
    {
        var rows = (orderItems ?? Array.Empty<(Guid, decimal, decimal, decimal)>()).ToList();
        var totalCost = rows.Sum(r => r.OrderedQty * r.AdditionalCostPerQty);
        if (totalCost == 0m)
        {
            // Costs that net to zero leave valuation unchanged, so keep their rows in full
            return 1m;
        }

        var pendingCost = 0m;
        var receivedCost = 0m;
        var hasFilter = receiptQtyByOrderRowId != null && receiptQtyByOrderRowId.Count > 0;
        foreach (var row in rows)
        {
            var pendingQty = Math.Max(row.OrderedQty - row.ReceivedQty, 0m);
            pendingCost += pendingQty * row.AdditionalCostPerQty;

            if (!hasFilter || receiptQtyByOrderRowId!.ContainsKey(row.OrderRowId))
            {
                var qtyToReceive = hasFilter && receiptQtyByOrderRowId!.TryGetValue(row.OrderRowId, out var explicitQty) && explicitQty > 0m
                    ? explicitQty
                    : pendingQty;
                receivedCost += qtyToReceive * row.AdditionalCostPerQty;
            }
        }

        if (pendingCost == 0m)
        {
            return 0m;
        }

        var remainingShare = Math.Max(1m - (additionalCostTakenByEarlierReceipts / totalCost), 0m);
        return remainingShare * Math.Min(receivedCost / pendingCost, 1m);
    }

    /// <summary>
    /// Allocates to the finished good row <c>(100 - allocatedPct) / 100</c> of its gross cost where
    /// <c>allocatedPct</c> sums ONLY the percentage co-products actually present with positive net qty on the receipt
    /// (giving the finished good the share of any BOM % co-products omitted or zero-qty on the receipt).
    /// Per ERPNext PR #67398 (<c>calculate_percentage_secondary_rows</c>).
    /// </summary>
    public static (decimal AllocatedCoProductPercentage, decimal FinishedGoodAmount, decimal FinishedGoodRate)
        CalculateFinishedGoodShareAfterReceiptPercentageCoProducts(
            decimal fgGrossAmount,
            decimal fgQty,
            IEnumerable<(decimal NetQty, decimal CostAllocationPercentage)>? receiptPercentageRows,
            int precision = 4)
    {
        var allocatedPct = 0m;
        if (receiptPercentageRows != null)
        {
            foreach (var (netQty, pct) in receiptPercentageRows)
            {
                if (netQty > 0m && pct > 0m)
                {
                    allocatedPct += pct;
                }
            }
        }

        var fgSharePct = Math.Max(0m, 100m - allocatedPct);
        var fgAmount = Math.Round(fgGrossAmount * (fgSharePct / 100m), precision, MidpointRounding.AwayFromZero);
        var fgRate = fgQty != 0m
            ? Math.Round(fgAmount / fgQty, precision, MidpointRounding.AwayFromZero)
            : 0m;

        return (allocatedPct, fgAmount, fgRate);
    }

    /// <summary>
    /// Validates cumulative transfer quantities per Subcontracting Inward Order row across all Stock Entry lines
    /// so splitting a return or delivery across multiple lines referencing the same SCIO row cannot bypass
    /// the row's maximum allowed quantity.
    /// Per ERPNext PR #67500 (<c>subcontracting_inward_controller.py</c>).
    /// </summary>
    public static void ValidateCumulativeInwardOrderQuantities(
        IEnumerable<(int RowNumber, Guid ScioDetailId, string ItemName, decimal TransferQty)>? rows,
        IReadOnlyDictionary<Guid, decimal>? maxAllowedByScioDetailId,
        int precision = 4)
    {
        if (rows == null || maxAllowedByScioDetailId == null)
        {
            return;
        }

        var cumulativeByRow = new Dictionary<Guid, decimal>();
        foreach (var row in rows)
        {
            var running = cumulativeByRow.GetValueOrDefault(row.ScioDetailId, 0m) + row.TransferQty;
            cumulativeByRow[row.ScioDetailId] = running;

            if (maxAllowedByScioDetailId.TryGetValue(row.ScioDetailId, out var maxAllowed)
                && Math.Round(running, precision, MidpointRounding.AwayFromZero)
                    > Math.Round(maxAllowed, precision, MidpointRounding.AwayFromZero))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("rowNumber", row.RowNumber)
                    .WithData("scioDetailId", row.ScioDetailId)
                    .WithData("detail", $"Row #{row.RowNumber}: Cumulative quantity ({running}) for Item '{row.ItemName}' exceeds allowed quantity ({maxAllowed}).");
            }
        }
    }

    /// <summary>
    /// Computes the finished good's cost share on a Subcontracting Receipt <em>return</em> from the
    /// returned receipt's original secondary rows: reserves ONLY the percentages of original rows with
    /// non-zero qty and <see cref="Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost"/>
    /// (excluding <c>ValuationRate</c> and <c>Manual</c> rows), so a return reverses at the original
    /// receipt's share (<c>100 - allocatedPct</c>) rather than the BOM's static share.
    /// Per ERPNext commits <c>e2d286d688</c> and <c>61bb973b59</c> (<c>get_returned_allocated_per</c>).
    /// </summary>
    public static (decimal AllocatedCoProductPercentage, decimal FinishedGoodAmount, decimal FinishedGoodRate)
        CalculateReturnedFinishedGoodShareFromReceiptRows(
            decimal fgReturnGrossAmount,
            decimal fgReturnQty,
            IEnumerable<(decimal OriginalQty, Manufacturing.SecondaryItemValuationType ValuationType, decimal CostAllocationPercentage)>? originalReceiptSecondaryRows,
            int precision = 4)
    {
        var allocatedPct = 0m;
        if (originalReceiptSecondaryRows != null)
        {
            foreach (var row in originalReceiptSecondaryRows)
            {
                if (row.OriginalQty != 0m
                    && row.ValuationType == Manufacturing.SecondaryItemValuationType.PercentageOfComponentCost
                    && row.CostAllocationPercentage > 0m)
                {
                    allocatedPct += row.CostAllocationPercentage;
                }
            }
        }

        var fgSharePct = Math.Max(0m, 100m - allocatedPct);
        var fgAmount = Math.Round(fgReturnGrossAmount * (fgSharePct / 100m), precision, MidpointRounding.AwayFromZero);
        var fgRate = fgReturnQty != 0m
            ? Math.Round(fgAmount / fgReturnQty, precision, MidpointRounding.AwayFromZero)
            : 0m;

        return (allocatedPct, fgAmount, fgRate);
    }

    /// <summary>
    /// Maps each Subcontracting Inward Order finished-good row to the Difference Account
    /// (<c>ExpenseAccountId</c>) used by its submitted <c>SubcontractingDelivery</c> entries when all
    /// deliveries for that row used one consistent account; returns no default if deliveries used
    /// multiple distinct accounts or none.
    /// Per ERPNext commits <c>acdd0590d1</c> and <c>bace342ab3</c> (<c>get_delivery_accounts</c>).
    /// </summary>
    public static Dictionary<Guid, Guid> ResolveSubcontractingReturnDifferenceAccounts(
        IEnumerable<(Guid ScioDetailId, Guid? ExpenseAccountId)>? submittedDeliveryRows)
    {
        var result = new Dictionary<Guid, Guid>();
        if (submittedDeliveryRows == null)
        {
            return result;
        }

        var accountsByRow = new Dictionary<Guid, HashSet<Guid?>>();
        foreach (var row in submittedDeliveryRows)
        {
            if (!accountsByRow.TryGetValue(row.ScioDetailId, out var set))
            {
                set = new HashSet<Guid?>();
                accountsByRow[row.ScioDetailId] = set;
            }
            set.Add(row.ExpenseAccountId);
        }

        foreach (var (scioDetailId, used) in accountsByRow)
        {
            if (used.Count == 1)
            {
                var single = used.First();
                if (single.HasValue && single.Value != Guid.Empty)
                {
                    result[scioDetailId] = single.Value;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Preserves an explicitly chosen Difference Account on a <c>SubcontractingDelivery</c> or
    /// <c>SubcontractingReturn</c> Stock Entry row, falling back to the delivery default (for returns)
    /// and then the company default expense account only when the row has no explicit account.
    /// Per ERPNext commit <c>5b56de6ee4</c> (<c>stock_entry.py</c> / <c>stock_entry_detail.py</c>).
    /// </summary>
    public static Guid? ResolveSubcontractingEntryExpenseAccount(
        Guid? explicitExpenseAccountId,
        Guid? deliveryDefaultAccountId,
        Guid? companyDefaultExpenseAccountId)
    {
        if (explicitExpenseAccountId.HasValue && explicitExpenseAccountId.Value != Guid.Empty)
        {
            return explicitExpenseAccountId;
        }

        if (deliveryDefaultAccountId.HasValue && deliveryDefaultAccountId.Value != Guid.Empty)
        {
            return deliveryDefaultAccountId;
        }

        return companyDefaultExpenseAccountId;
    }

    /// <summary>
    /// Allocates subcontracting raw-material consumption across Subcontracting Order supplied-item rows:
    /// each row's positive net sent quantity absorbs its own finished good's consumption first, and then
    /// absorbs any consumption of the same RM item on the same order that exceeded what another finished
    /// good's rows were sent (<c>consumed_beyond_sent</c>).
    /// Per ERPNext commit <c>3fd104156f</c> (<c>get_consumed_qty_beyond_sent</c> in <c>subcontracting_controller.py</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> AllocateSubcontractingRmConsumptionAcrossOrderRows(
        IEnumerable<(Guid OrderRowId, Guid OrderId, Guid MainItemId, Guid RmItemId, decimal NetSentQty)>? orderRows,
        IReadOnlyDictionary<(Guid RmItemId, Guid MainItemId, Guid OrderId), decimal>? itemwiseConsumedQty,
        int precision = 4)
    {
        var allocated = new Dictionary<Guid, decimal>();
        if (orderRows == null)
        {
            return allocated;
        }

        var rows = orderRows.ToList();
        var sentFor = new Dictionary<(Guid RmItemId, Guid MainItemId, Guid OrderId), decimal>();
        foreach (var row in rows)
        {
            var key = (row.RmItemId, row.MainItemId, row.OrderId);
            sentFor[key] = sentFor.GetValueOrDefault(key, 0m) + Math.Max(row.NetSentQty, 0m);
        }

        var ownRemaining = new Dictionary<(Guid RmItemId, Guid MainItemId, Guid OrderId), decimal>();
        var beyondSentRemaining = new Dictionary<(Guid RmItemId, Guid OrderId), decimal>();
        if (itemwiseConsumedQty != null)
        {
            foreach (var (key, consumed) in itemwiseConsumedQty)
            {
                ownRemaining[key] = consumed;
                var sent = sentFor.GetValueOrDefault(key, 0m);
                var overflow = Math.Max(consumed - sent, 0m);
                if (overflow > 0m)
                {
                    var orderKey = (key.RmItemId, key.OrderId);
                    beyondSentRemaining[orderKey] = beyondSentRemaining.GetValueOrDefault(orderKey, 0m) + overflow;
                }
            }
        }

        foreach (var row in rows)
        {
            var qtyLeft = Math.Max(row.NetSentQty, 0m);
            var rowConsumed = 0m;

            var ownKey = (row.RmItemId, row.MainItemId, row.OrderId);
            if (qtyLeft > 0m && ownRemaining.TryGetValue(ownKey, out var ownAvail) && ownAvail > 0m)
            {
                var take = Math.Min(ownAvail, qtyLeft);
                ownRemaining[ownKey] = ownAvail - take;
                qtyLeft -= take;
                rowConsumed += take;
            }

            var overflowKey = (row.RmItemId, row.OrderId);
            if (qtyLeft > 0m && beyondSentRemaining.TryGetValue(overflowKey, out var overflowAvail) && overflowAvail > 0m)
            {
                var take = Math.Min(overflowAvail, qtyLeft);
                beyondSentRemaining[overflowKey] = overflowAvail - take;
                qtyLeft -= take;
                rowConsumed += take;
            }

            allocated[row.OrderRowId] = Math.Round(rowConsumed, precision, MidpointRounding.AwayFromZero);
        }

        return allocated;
    }

    /// <summary>
    /// Computes the Subcontracting Inward Order conversion ratio (<c>soStockQty / soFgItemQty</c>)
    /// from its linked Sales Order row ONLY when BOTH <paramref name="salesOrderStockQty"/> and
    /// <paramref name="salesOrderFgItemQty"/> are non-zero, keeping <paramref name="existingRatio"/>
    /// when the Sales Order row quantity is zero so save/submit never divides by zero.
    /// Per ERPNext commit <c>a36fc2fd7a</c> (<c>subcontracting_inward_order.py</c>).
    /// </summary>
    public static decimal ResolveInwardOrderSalesConversionFactor(
        decimal existingRatio,
        decimal salesOrderStockQty,
        decimal salesOrderFgItemQty)
    {
        if (salesOrderStockQty != 0m && salesOrderFgItemQty != 0m)
        {
            return salesOrderStockQty / salesOrderFgItemQty;
        }

        return existingRatio;
    }

    /// <summary>
    /// Resolves the cost-allocation percentage attributed to the finished good row on a Subcontracting Receipt:
    /// - On a return (<paramref name="isReturn"/> = true), uses <paramref name="bomCostAllocationPercentage"/>
    ///   so the return gives back the finished good at its receipt share.
    /// - On a regular receipt, uses <c>100 - sum(receiptPercentageSecondaryAllocations)</c> so the share of any
    ///   percentage-based co-products omitted from the receipt is absorbed by the finished good instead of lost.
    /// Per ERPNext commit <c>7938233f41</c> (<c>subcontracting_receipt.py</c>).
    /// </summary>
    public static decimal ResolveFinishedGoodCostAllocationPercentage(
        bool isReturn,
        decimal bomCostAllocationPercentage,
        IEnumerable<decimal>? receiptPercentageSecondaryAllocations)
    {
        if (isReturn)
        {
            return bomCostAllocationPercentage;
        }

        var allocatedSecondaryPct = receiptPercentageSecondaryAllocations?.Sum() ?? 0m;
        return 100m - allocatedSecondaryPct;
    }

    /// <summary>
    /// Refuses submitting or re-opening a Subcontracting Order when any row with remaining qty to receive
    /// (<c>ReceivedQty &lt; Qty</c>) references a closed Purchase Order item row.
    /// Per ERPNext commits <c>4b0cbc8ec1</c> and <c>7f085027e1</c> (<c>subcontracting_order.py</c>).
    /// </summary>
    public static void ValidateSubcontractingOrderPurchaseOrderRowsOpen(
        string purchaseOrderNumber,
        IEnumerable<(int RowIdx, Guid PurchaseOrderItemId, decimal Qty, decimal ReceivedQty)>? scoItems,
        IReadOnlyDictionary<Guid, (string ItemCode, bool IsClosed)>? poItemsById)
    {
        if (scoItems == null || poItemsById == null)
        {
            return;
        }

        foreach (var item in scoItems)
        {
            if (item.ReceivedQty < item.Qty
                && poItemsById.TryGetValue(item.PurchaseOrderItemId, out var poItem)
                && poItem.IsClosed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{item.RowIdx}: Item {poItem.ItemCode} is closed in Purchase Order {purchaseOrderNumber} and cannot be processed further.");
            }
        }
    }

    /// <summary>
    /// Validates cumulative delivery or return quantities across ALL rows referencing the same
    /// Subcontracting Inward Order row, rounding both the summed transfer quantity and maximum allowed
    /// quantity to <paramref name="qtyPrecision"/> before comparing so split rows cannot bypass the cap
    /// and sub-precision float dust does not trigger false rejections.
    /// Per ERPNext commits <c>a7fe007c52</c> and <c>4311634e8e</c> (<c>subcontracting_inward_controller.py</c>).
    /// </summary>
    public static void ValidateInwardOrderCumulativeRowQuantities(
        IEnumerable<(int RowIdx, Guid ScioDetailId, string ItemCode, decimal TransferQty)>? rows,
        IReadOnlyDictionary<Guid, decimal>? maxAllowedQtyByScioDetailId,
        string orderNumber,
        int qtyPrecision = 4)
    {
        if (rows == null || maxAllowedQtyByScioDetailId == null)
        {
            return;
        }

        var cumulativeByDetail = new Dictionary<Guid, decimal>();
        foreach (var row in rows)
        {
            var running = cumulativeByDetail.GetValueOrDefault(row.ScioDetailId, 0m) + row.TransferQty;
            cumulativeByDetail[row.ScioDetailId] = running;

            if (maxAllowedQtyByScioDetailId.TryGetValue(row.ScioDetailId, out var maxAllowed))
            {
                var roundedRunning = Math.Round(running, qtyPrecision, MidpointRounding.AwayFromZero);
                var roundedMax = Math.Round(maxAllowed, qtyPrecision, MidpointRounding.AwayFromZero);
                if (roundedRunning > roundedMax)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{row.RowIdx}: Quantity of Item {row.ItemCode} cannot be more than {roundedMax} against Subcontracting Inward Order {orderNumber}.");
                }
            }
        }
    }

    /// <summary>
    /// Computes the row amount used for additional-cost weighting, stripping any previously applied
    /// additional cost so re-saving preserves identical weights.
    /// Per ERPNext PR #68870 (<c>SubcontractingReceipt.get_amount_for_costing</c>).
    /// </summary>
    public static decimal GetSubcontractingReceiptAmountForCosting(
        decimal rowAmount,
        decimal qtyForCosting,
        decimal previousAdditionalCostPerQty,
        decimal bomCostAllocationPercentage = 100m)
    {
        var allocation = bomCostAllocationPercentage > 0m ? bomCostAllocationPercentage / 100m : 1m;
        var priorAdditionalCost = qtyForCosting * previousAdditionalCostPerQty * allocation;
        return rowAmount - priorAdditionalCost;
    }

    /// <summary>
    /// Distributes Subcontracting Receipt additional costs where individual cost rows may target a
    /// specific finished good row (<c>TargetItemRowId</c>), and the remaining untargeted cost is spread
    /// across finished goods by <c>Amount</c> (pre-additional-cost) or <c>Qty</c> with running-total rounding.
    /// Per ERPNext PR #68870 (<c>SubcontractingController.calculate_additional_costs</c>).
    /// </summary>
    public static List<(Guid RowId, decimal TotalShare, decimal AdditionalCostPerQty)> CalculateTargetedAndSpreadAdditionalCosts(
        string distributeBasedOn,
        IReadOnlyList<(Guid RowId, bool IsSecondaryItem, decimal AmountForCosting, decimal QtyForCosting)> rows,
        IReadOnlyList<(int RowIdx, Guid? TargetItemRowId, decimal Amount)> additionalCostRows,
        int precision = 2)
    {
        var fgRows = (rows ?? Array.Empty<(Guid, bool, decimal, decimal)>()).Where(r => !r.IsSecondaryItem).ToList();
        var result = new List<(Guid RowId, decimal TotalShare, decimal AdditionalCostPerQty)>(fgRows.Count);
        if (fgRows.Count == 0)
            return result;

        var rowIndex = new Dictionary<Guid, int>(fgRows.Count);
        for (var i = 0; i < fgRows.Count; i++)
            rowIndex[fgRows[i].RowId] = i;

        var costs = new decimal[fgRows.Count];
        decimal totalAdditionalCosts = 0m;
        decimal targetedSum = 0m;

        if (additionalCostRows != null)
        {
            foreach (var costRow in additionalCostRows)
            {
                totalAdditionalCosts += costRow.Amount;
                if (!costRow.TargetItemRowId.HasValue || costRow.TargetItemRowId.Value == Guid.Empty)
                    continue;

                if (!rowIndex.TryGetValue(costRow.TargetItemRowId.Value, out var idx))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{costRow.RowIdx}: Additional Costs row targets an item row that is not a finished good.");
                }

                costs[idx] += costRow.Amount;
                targetedSum += costRow.Amount;
            }
        }

        var spreadCost = Math.Round(totalAdditionalCosts - targetedSum, precision, MidpointRounding.AwayFromZero);
        if (spreadCost != 0m)
        {
            var spreadInput = fgRows.Select(r => (r.RowId, false, r.AmountForCosting, r.QtyForCosting)).ToList();
            var spreadShares = CalculateAdditionalCostPerQtyForFinishedGoods(spreadCost, distributeBasedOn, spreadInput, precision);
            for (var i = 0; i < fgRows.Count; i++)
                costs[i] += spreadShares[i].ShareAmount;
        }

        for (var i = 0; i < fgRows.Count; i++)
        {
            var fg = fgRows[i];
            var perQty = fg.QtyForCosting != 0m ? costs[i] / fg.QtyForCosting : 0m;
            result.Add((fg.RowId, costs[i], perQty));
        }

        return result;
    }

    /// <summary>
    /// Computes returned additional cost amounts on a Subcontracting Receipt return:
    /// targeted cost rows scale strictly by their own FG row's return ratio, while untargeted cost rows
    /// scale by the remaining untargeted ratio.
    /// Per ERPNext PR #68870 (<c>SubcontractingReceipt.set_return_additional_costs</c>).
    /// </summary>
    public static List<(int RowIdx, Guid? TargetItemRowId, decimal ReturnedAmount)> CalculateSubcontractingReturnAdditionalCosts(
        IReadOnlyDictionary<Guid, (decimal OriginalQty, decimal QtyForCosting, decimal AdditionalCostPerQty, decimal BasisValue)> originalFgRows,
        IEnumerable<(Guid OriginalRowId, decimal ReturnQty)> returnRows,
        IReadOnlyList<(int RowIdx, Guid? TargetItemRowId, decimal Amount)> originalAdditionalCosts,
        int precision = 2)
    {
        var result = new List<(int RowIdx, Guid? TargetItemRowId, decimal ReturnedAmount)>();
        if (originalAdditionalCosts == null || originalAdditionalCosts.Count == 0)
            return result;

        var totalCost = originalFgRows.Values.Sum(r => r.QtyForCosting * r.AdditionalCostPerQty);
        var totalBasis = originalFgRows.Values.Sum(r => r.BasisValue);

        decimal returnedCost = 0m;
        decimal returnedBasis = 0m;
        var returnedRatios = new Dictionary<Guid, decimal>();

        foreach (var ret in returnRows ?? Enumerable.Empty<(Guid, decimal)>())
        {
            if (!originalFgRows.TryGetValue(ret.OriginalRowId, out var orig) || orig.OriginalQty == 0m)
                continue;

            var rowRatio = ret.ReturnQty / orig.OriginalQty;
            returnedCost += ret.ReturnQty * orig.AdditionalCostPerQty;
            returnedBasis += ret.ReturnQty * orig.BasisValue / orig.OriginalQty;
            returnedRatios[ret.OriginalRowId] = returnedRatios.GetValueOrDefault(ret.OriginalRowId, 0m) + rowRatio;
        }

        var targetedRows = originalAdditionalCosts
            .Where(r => r.TargetItemRowId.HasValue && r.TargetItemRowId.Value != Guid.Empty)
            .ToList();
        var untargetedCost = Math.Round(totalCost - targetedRows.Sum(r => r.Amount), precision, MidpointRounding.AwayFromZero);

        decimal untargetedRatio;
        if (untargetedCost != 0m)
        {
            var targetedReturned = targetedRows.Sum(r =>
                r.Amount * returnedRatios.GetValueOrDefault(r.TargetItemRowId!.Value, 0m));
            untargetedRatio = (returnedCost - targetedReturned) / untargetedCost;
        }
        else
        {
            untargetedRatio = totalBasis != 0m ? returnedBasis / totalBasis : 0m;
        }

        foreach (var costRow in originalAdditionalCosts)
        {
            var ratio = costRow.TargetItemRowId.HasValue && costRow.TargetItemRowId.Value != Guid.Empty
                ? returnedRatios.GetValueOrDefault(costRow.TargetItemRowId.Value, 0m)
                : untargetedRatio;
            var amount = Math.Round(costRow.Amount * ratio, precision, MidpointRounding.AwayFromZero);
            result.Add((costRow.RowIdx, costRow.TargetItemRowId, amount));
        }

        return result;
    }

    /// <summary>
    /// Validates Manufacture Stock Entry consumption of Customer Provided Items in a Subcontracting Inward Order:
    /// permits multiple rows of the same item, summing <c>TransferQty</c> per item and rounding both sides
    /// to <paramref name="qtyPrecision"/> before checking against <c>AvailableQty</c>.
    /// Per ERPNext PR #67501 (<c>subcontracting_inward_controller.py</c>).
    /// </summary>
    public static void ValidateInwardManufactureMultiRowConsumption(
        IEnumerable<(int RowIdx, Guid ItemId, string ItemCode, decimal TransferQty)>? rows,
        IReadOnlyDictionary<Guid, (decimal ConsumedQty, decimal AvailableQty)>? availableByItemId,
        int qtyPrecision = 4)
    {
        if (rows == null || availableByItemId == null)
            return;

        var summedByItem = new Dictionary<Guid, decimal>();
        foreach (var row in rows)
        {
            if (!availableByItemId.TryGetValue(row.ItemId, out var stock))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowIdx}: Customer Provided Item {row.ItemCode} is not part of the Subcontracting Inward Order.");
            }

            var cumulative = summedByItem.GetValueOrDefault(row.ItemId, 0m) + row.TransferQty;
            summedByItem[row.ItemId] = cumulative;

            var totalConsumed = Math.Round(stock.ConsumedQty + cumulative, qtyPrecision, MidpointRounding.AwayFromZero);
            var bound = Math.Round(stock.AvailableQty, qtyPrecision, MidpointRounding.AwayFromZero);
            if (totalConsumed > bound)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowIdx}: Cannot consume {totalConsumed} of Item {row.ItemCode}; available quantity is {bound}.");
            }
        }
    }

    /// <summary>
    /// Validates cancellation of a Subcontracting Inward Manufacture Stock Entry against billed own extra material:
    /// scopes extra-material lookup strictly to <c>(ScioId, FgItemId, RmItemId, SourceWarehouseId)</c>,
    /// sums duplicate rows of the same <c>(RmItemId, SourceWarehouseId)</c>, and compares at <paramref name="qtyPrecision"/>.
    /// Per ERPNext PR #67501 (<c>subcontracting_inward_controller.py</c>).
    /// </summary>
    public static void ValidateInwardManufactureCancelAgainstBilledExtraMaterial(
        Guid scioId,
        Guid fgItemId,
        IEnumerable<(int RowIdx, Guid ItemId, string ItemCode, Guid SourceWarehouseId, decimal TransferQty)>? ownMaterialRows,
        IReadOnlyDictionary<(Guid ScioId, Guid FgItemId, Guid ItemId, Guid WarehouseId), (decimal ConsumedQty, decimal BilledQty, decimal ReturnedQty)>? scioReceivedOwnMaterials,
        int qtyPrecision = 4)
    {
        if (ownMaterialRows == null || scioReceivedOwnMaterials == null)
            return;

        var consumedByKey = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();
        foreach (var row in ownMaterialRows)
        {
            var lookupKey = (scioId, fgItemId, row.ItemId, row.SourceWarehouseId);
            if (!scioReceivedOwnMaterials.TryGetValue(lookupKey, out var rec))
                continue;

            var aggKey = (row.ItemId, row.SourceWarehouseId);
            var summed = consumedByKey.GetValueOrDefault(aggKey, 0m) + row.TransferQty;
            consumedByKey[aggKey] = summed;

            var netBilled = Math.Round(rec.BilledQty - rec.ReturnedQty, qtyPrecision, MidpointRounding.AwayFromZero);
            var remainingConsumed = Math.Round(rec.ConsumedQty - summed, qtyPrecision, MidpointRounding.AwayFromZero);
            if (netBilled > remainingConsumed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowIdx}: Cannot cancel this Manufacturing Stock Entry as billed quantity of Item {row.ItemCode} cannot be greater than consumed quantity.");
            }
        }
    }

    /// <summary>
    /// Splits a Return of Components over the subcontracting order rows its raw material was sent on,
    /// allocating each row up to what it still has at the supplier (<c>TotalSuppliedQty - ConsumedQty</c>),
    /// rounding each row's qty (including the last row) to <paramref name="qtyPrecision"/>, and slicing
    /// serial numbers proportionally per ERPNext PR #68828 (<c>add_return_items</c>, <c>get_qty_left_on_order_rows</c>).
    /// </summary>
    public static List<(Guid OrderRowId, decimal Qty, List<string> SerialNos)> SplitSubcontractingComponentReturnAcrossOrderRows(
        decimal returnQty,
        IReadOnlyList<(Guid OrderRowId, decimal TotalSuppliedQty, decimal ConsumedQty)> orderRows,
        IReadOnlyList<string>? serialNos = null,
        int qtyPrecision = 3)
    {
        var allocations = new List<(Guid OrderRowId, decimal Qty, List<string> SerialNos)>();
        if (returnQty <= 0m || orderRows == null || orderRows.Count == 0)
            return allocations;

        var remaining = returnQty;
        var serialOffset = 0;

        for (var i = 0; i < orderRows.Count; i++)
        {
            var row = orderRows[i];
            var isLast = i == orderRows.Count - 1;
            var qtyLeft = Math.Max(row.TotalSuppliedQty - row.ConsumedQty, 0m);
            var rawQty = isLast ? remaining : Math.Min(remaining, qtyLeft);
            var rowQty = Math.Round(rawQty, qtyPrecision, MidpointRounding.AwayFromZero);

            if (rowQty <= 0m)
                continue;

            remaining -= rowQty;

            var rowSerials = new List<string>();
            if (serialNos != null && serialOffset < serialNos.Count)
            {
                var takeCount = Math.Min((int)rowQty, serialNos.Count - serialOffset);
                for (var s = 0; s < takeCount; s++)
                    rowSerials.Add(serialNos[serialOffset + s]);
                serialOffset += takeCount;
            }

            allocations.Add((row.OrderRowId, rowQty, rowSerials));
        }

        return allocations;
    }

    /// <summary>
    /// Computes the share of a Subcontracting Order's additional costs that a mapped Subcontracting Receipt
    /// should take: what earlier submitted receipts left of them, in proportion to the qty this receipt takes
    /// of what is still pending (ERPNext PR #67357: <c>get_additional_cost_share</c>).
    /// - When order additional costs net to zero (<c>totalCost == 0</c>), returns <c>1.0</c> so offsetting
    ///   expense rows are preserved in full rather than scaled to zero.
    /// - Uses ratio <c>max(1 - taken / totalCost, 0)</c> so negative net additional costs are preserved rather
    ///   than clamped to zero.
    /// </summary>
    public static decimal CalculateReceiptAdditionalCostShare(
        IReadOnlyList<(Guid RowId, decimal Qty, decimal ReceivedQty, decimal AdditionalCostPerQty)> orderRows,
        decimal costTakenByPriorReceipts,
        IReadOnlyDictionary<Guid, decimal>? selectedQtyByRow = null)
    {
        if (orderRows == null || orderRows.Count == 0)
            return 0m;

        var totalCost = orderRows.Sum(r => r.Qty * r.AdditionalCostPerQty);
        if (totalCost == 0m)
            return 1m;

        decimal pendingCost = 0m;
        decimal receivedCost = 0m;
        var hasSelection = selectedQtyByRow != null && selectedQtyByRow.Count > 0;

        foreach (var row in orderRows)
        {
            var pendingQty = Math.Max(row.Qty - row.ReceivedQty, 0m);
            pendingCost += pendingQty * row.AdditionalCostPerQty;

            if (!hasSelection)
            {
                receivedCost += pendingQty * row.AdditionalCostPerQty;
            }
            else if (selectedQtyByRow!.TryGetValue(row.RowId, out var selectedQty))
            {
                var effectiveQty = selectedQty > 0m ? selectedQty : pendingQty;
                receivedCost += effectiveQty * row.AdditionalCostPerQty;
            }
        }

        if (pendingCost == 0m)
            return 0m;

        var remainingShare = Math.Max(1m - (costTakenByPriorReceipts / totalCost), 0m);
        var proportion = Math.Min(receivedCost / pendingCost, 1m);
        return remainingShare * proportion;
    }
}

public sealed record SubcontractingSecondaryRowInput(
    Guid RowId,
    Guid ItemId,
    Guid? WarehouseId,
    decimal Qty,
    Manufacturing.SecondaryItemValuationType ValuationType,
    decimal CostAllocationPercentage = 0m,
    decimal BomSecondaryBaseCost = 0m,
    decimal BomSecondaryStockQty = 0m);

public sealed record SubcontractingReceiptCostingResult(
    decimal RmCostPerQty,
    decimal SecondaryItemsCostPerQty,
    decimal FinishedGoodRate,
    IReadOnlyDictionary<Guid, (decimal Rate, decimal Amount)> SecondaryRatesByRowId);

public sealed record SubcontractingReservationPlanHandoff(
    Guid SuppliedItemId,
    Guid RmItemId,
    Guid? ReserveWarehouseId,
    decimal RequiredQty,
    Guid ProductionPlanId,
    Guid ProductionPlanMrItemId);

public sealed record SubcontractingDirectReservationItem(
    Guid SuppliedItemId,
    Guid RmItemId,
    Guid? ReserveWarehouseId,
    decimal RequiredQty);

public class SubcontractingRmConsumption
{
    public Guid ItemId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
}
