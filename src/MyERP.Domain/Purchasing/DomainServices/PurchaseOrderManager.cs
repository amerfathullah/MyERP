using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Purchase Order business rules.
/// Validates supplier eligibility, minimum order quantities, and manages
/// fulfillment-related side effects (Bin ordered qty, MR tracking).
/// </summary>
public class PurchaseOrderManager : DomainService
{
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<MaterialRequest, Guid> _mrRepository;

    public PurchaseOrderManager(
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<MaterialRequest, Guid> mrRepository)
    {
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _mrRepository = mrRepository;
    }

    /// <summary>
    /// Validates supplier is eligible for purchase orders.
    /// Checks hold type (All or Invoices blocks PO — "Invoices" blocks PI/PO creation,
    /// "Payments" blocks Payment Entry only) and scorecard enforcement (PreventPurchaseOrders).
    /// Must be called before PO submission.
    /// </summary>
    public async Task ValidateSupplierEligibilityAsync(Guid supplierId)
    {
        var supplier = await _supplierRepository.GetAsync(supplierId);

        if (supplier.HoldType == SupplierHoldType.All || supplier.HoldType == SupplierHoldType.Invoices)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                .WithData("supplierName", supplier.Name)
                .WithData("holdType", supplier.HoldType.ToString());
        }

        if (supplier.PreventPurchaseOrders)
        {
            throw new BusinessException("MyERP:04006")
                .WithData("supplierName", supplier.Name);
        }
    }

    /// <summary>
    /// Validates each PO item meets the item's minimum order quantity.
    /// Per DO-NOT: "Validate PO minimum order qty per row — must aggregate stock_qty
    /// across ALL rows per item before comparing to Item.min_order_qty"
    /// Per ERPNext: hard error, not warning.
    /// </summary>
    public async Task ValidateMinimumOrderQtyAsync(PurchaseOrder order)
    {
        // Aggregate quantity by item (multiple rows for same item are summed)
        var qtyByItem = order.Items
            .GroupBy(i => i.ItemId)
            .Select(g => new { ItemId = g.Key, TotalQty = g.Sum(i => i.Quantity) })
            .ToList();

        foreach (var group in qtyByItem)
        {
            var item = await _itemRepository.FindAsync(group.ItemId);
            if (item != null && item.MinOrderQty > 0 && group.TotalQty < item.MinOrderQty)
            {
                throw new BusinessException("MyERP:04005")
                    .WithData("itemName", item.ItemName)
                    .WithData("minQty", item.MinOrderQty)
                    .WithData("orderedQty", group.TotalQty);
            }
        }
    }

    /// <summary>
    /// Updates Material Request items' OrderedQuantity when PO is submitted.
    /// Increments ordered qty for each PO item linked to an MR item.
    /// </summary>
    public async Task UpdateMaterialRequestOrderedQtyAsync(PurchaseOrder order, bool reverse = false)
    {
        var mrItemIds = order.Items
            .Where(i => i.MaterialRequestItemId.HasValue)
            .Select(i => i.MaterialRequestItemId!.Value)
            .ToList();

        if (!mrItemIds.Any()) return;

        var mrQuery = await _mrRepository.GetQueryableAsync();
        var affectedMRs = mrQuery
            .Where(mr => mr.Items.Any(i => mrItemIds.Contains(i.Id)))
            .ToList();

        foreach (var mr in affectedMRs)
        {
            foreach (var poItem in order.Items.Where(i => i.MaterialRequestItemId.HasValue))
            {
                var mrItem = mr.Items.FirstOrDefault(i => i.Id == poItem.MaterialRequestItemId!.Value);
                if (mrItem != null)
                {
                    var delta = reverse ? -poItem.Quantity : poItem.Quantity;
                    mrItem.OrderedQuantity = Math.Max(0, mrItem.OrderedQuantity + delta);
                }
            }
            await _mrRepository.UpdateAsync(mr);
        }
    }

    /// <summary>
    /// Validates a Purchase Receipt item does not exceed the PO's allowed receipt qty including tolerance.
    /// Per ERPNext: max_allowed = ordered_qty × (1 + allowance_pct / 100) - already_received.
    /// The allowance comes from Company.OverDeliveryReceiptAllowance (Stock Settings in ERPNext).
    /// </summary>
    public void ValidateReceiptQty(PurchaseOrder order, Guid itemId, decimal receiptQty, decimal overReceiptAllowancePct = 0m)
    {
        var poItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (poItem == null) return;

        var maxAllowedTotal = poItem.Quantity * (1m + overReceiptAllowancePct / 100m);
        var remainingAllowed = maxAllowedTotal - poItem.ReceivedQty;

        if (receiptQty > remainingAllowed)
        {
            throw new BusinessException("MyERP:08006")
                .WithData("itemName", poItem.Description)
                .WithData("orderedQty", poItem.Quantity)
                .WithData("receivedQty", poItem.ReceivedQty)
                .WithData("attemptedQty", receiptQty);
        }
    }

    /// <summary>
    /// Validates a Purchase Invoice item does not exceed the PO's pending billing qty,
    /// including the company's over-billing tolerance percentage.
    /// Per ERPNext: max_allowed = ordered_qty × (1 + allowance_pct / 100).
    /// </summary>
    public void ValidateBillingQty(PurchaseOrder order, Guid itemId, decimal billingQty, decimal overBillingAllowancePct = 0m)
    {
        var poItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (poItem == null) return;

        var maxAllowedTotal = poItem.Quantity * (1m + overBillingAllowancePct / 100m);
        if (poItem.BilledQty + billingQty > maxAllowedTotal)
        {
            throw new BusinessException("MyERP:08007")
                .WithData("itemName", poItem.Description)
                .WithData("orderedQty", poItem.Quantity)
                .WithData("billedQty", poItem.BilledQty)
                .WithData("attemptedQty", billingQty);
        }
    }

    /// <summary>
    /// Checks whether the PO has any submitted dependent documents (PR or PI)
    /// that block cancellation. Per DO-NOT: must cancel children first.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        PurchaseOrder order,
        IRepository<PurchaseReceipt, Guid> prRepository,
        IRepository<PurchaseInvoice, Guid> piRepository,
        IRepository<SubcontractingOrder, Guid>? scoRepository = null)
    {
        var prQuery = await prRepository.GetQueryableAsync();
        var hasSubmittedPR = prQuery.Any(pr =>
            pr.PurchaseOrderId == order.Id
            && pr.Status != Core.DocumentStatus.Draft
            && pr.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedPR)
        {
            throw new BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Order")
                .WithData("dependent", "Purchase Receipt");
        }

        var piQuery = await piRepository.GetQueryableAsync();
        var poItemIds = order.Items.Select(oi => oi.Id).ToList();
        var hasSubmittedPI = piQuery.Any(pi =>
            pi.Items.Any(i => i.PurchaseOrderItemId.HasValue && poItemIds.Contains(i.PurchaseOrderItemId.Value))
            && pi.Status != Core.DocumentStatus.Draft
            && pi.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedPI)
        {
            throw new BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Order")
                .WithData("dependent", "Purchase Invoice");
        }

        // Per DO-NOT: "Allow SO/PO item update when Subcontracting Order already exists (must cancel SCO first)"
        if (scoRepository != null)
        {
            var scoQuery = await scoRepository.GetQueryableAsync();
            var hasActiveSCO = scoQuery.Any(sco =>
                sco.PurchaseOrderId == order.Id
                && sco.Status != SubcontractingOrderStatus.Draft
                && sco.Status != SubcontractingOrderStatus.Cancelled);

            if (hasActiveSCO)
            {
                throw new BusinessException("MyERP:01010")
                    .WithData("documentType", "Purchase Order")
                    .WithData("dependent", "Subcontracting Order");
            }
        }
    }

    /// <summary>
    /// Auto-fills per-item expected delivery dates from Item.LeadTimeDays when not explicitly set.
    /// Per ERPNext: each PO item's expected_delivery_date defaults to order_date + item.lead_time_days.
    /// Items with LeadTimeDays=0 fall back to the parent PO's ExpectedDeliveryDate.
    /// </summary>
    public async Task AutoFillExpectedDeliveryDatesAsync(PurchaseOrder order)
    {
        var itemIds = order.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var leadTimes = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.LeadTimeDays })
            .ToDictionary(i => i.Id, i => i.LeadTimeDays);

        foreach (var poItem in order.Items)
        {
            if (poItem.ExpectedDeliveryDate.HasValue) continue;

            if (leadTimes.TryGetValue(poItem.ItemId, out var leadDays) && leadDays > 0)
            {
                poItem.ExpectedDeliveryDate = order.OrderDate.AddDays(leadDays);
            }
        }
    }

    /// <summary>
    /// Calculates the aggregate overdue summary for a PO.
    /// Returns count of overdue items, most overdue days, and total pending qty of overdue items.
    /// </summary>
    public static PurchaseOrderOverdueSummary GetOverdueSummary(PurchaseOrder order, DateTime asOfDate)
    {
        var overdueItems = order.Items
            .Where(i => i.IsOverdue(asOfDate, order.ExpectedDeliveryDate))
            .ToList();

        return new PurchaseOrderOverdueSummary
        {
            OverdueItemCount = overdueItems.Count,
            MaxDaysOverdue = overdueItems.Count > 0
                ? overdueItems.Max(i => i.DaysOverdue(asOfDate, order.ExpectedDeliveryDate))
                : 0,
            TotalPendingOverdueQty = overdueItems.Sum(i => i.PendingReceiptQty),
            CriticalItems = overdueItems
                .Where(i => i.DaysOverdue(asOfDate, order.ExpectedDeliveryDate) > 7)
                .Select(i => new OverdueItemInfo
                {
                    ItemId = i.ItemId,
                    Description = i.Description,
                    DaysOverdue = i.DaysOverdue(asOfDate, order.ExpectedDeliveryDate),
                    PendingQty = i.PendingReceiptQty
                })
                .OrderByDescending(i => i.DaysOverdue)
                .ToList()
        };
    }
}

/// <summary>Summary of overdue items in a Purchase Order.</summary>
public class PurchaseOrderOverdueSummary
{
    public int OverdueItemCount { get; set; }
    public int MaxDaysOverdue { get; set; }
    public decimal TotalPendingOverdueQty { get; set; }
    public List<OverdueItemInfo> CriticalItems { get; set; } = [];
    public bool HasCriticalItems => CriticalItems.Count > 0;
}

/// <summary>Individual overdue item detail.</summary>
public class OverdueItemInfo
{
    public Guid ItemId { get; set; }
    public string Description { get; set; } = "";
    public int DaysOverdue { get; set; }
    public decimal PendingQty { get; set; }
}
