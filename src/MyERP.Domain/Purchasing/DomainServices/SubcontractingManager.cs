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
            foreach (var itemId in itemIds)
            {
                var poItem = po.Items.FirstOrDefault(i => i.ItemId == itemId);
                if (poItem != null && poItem.IsClosed)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.LinkedPurchaseOrderClosed)
                        .WithData("orderNumber", po.OrderNumber)
                        .WithData("itemId", itemId)
                        .WithData("detail", $"Cannot create a Subcontracting Order for closed Purchase Order item '{poItem.Description}'.");
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
    /// </summary>
    public SubcontractingRmConsumption[] CalculateRmConsumption(
        SubcontractingOrder sco, decimal receivedFgQty, string? backflushBasedOn = "BOM", bool isReturn = false)
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
                return new SubcontractingRmConsumption
                {
                    ItemId = si.ItemId,
                    RequiredQty = si.RequiredQty,
                    ConsumedQty = Math.Round(baseQty * ratio, 4),
                    WarehouseId = si.ReserveWarehouseId,
                    BatchId = si.BatchId
                };
            })
            .ToArray();
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
}

public class SubcontractingRmConsumption
{
    public Guid ItemId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
}
