using System;
using System.Linq;
using System.Threading.Tasks;
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

        foreach (var receiptItem in receipt.Items)
        {
            var scoItem = sco.Items.FirstOrDefault(i => i.ItemId == receiptItem.ItemId);
            if (scoItem == null) continue;

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
    /// consumed_qty = supplied_item.required_qty × (received_fg_qty / total_fg_qty)
    /// Per ERPNext subcontracting_controller: proportional consumption.
    /// </summary>
    public SubcontractingRmConsumption[] CalculateRmConsumption(
        SubcontractingOrder sco, decimal receivedFgQty)
    {
        // Total FG qty ordered
        var totalFgQty = sco.Items.Sum(i => i.Qty);
        if (totalFgQty <= 0) return Array.Empty<SubcontractingRmConsumption>();

        var ratio = receivedFgQty / totalFgQty;

        return sco.SuppliedItems
            .Select(si => new SubcontractingRmConsumption
            {
                ItemId = si.ItemId,
                RequiredQty = si.RequiredQty,
                ConsumedQty = Math.Round(si.RequiredQty * ratio, 4),
                WarehouseId = si.ReserveWarehouseId
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
}
