using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
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
}

public class SubcontractingRmConsumption
{
    public Guid ItemId { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
}
