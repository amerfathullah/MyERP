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
    /// Checks hold type (All blocks PO) and scorecard enforcement (PreventPurchaseOrders).
    /// Must be called before PO submission.
    /// </summary>
    public async Task ValidateSupplierEligibilityAsync(Guid supplierId)
    {
        var supplier = await _supplierRepository.GetAsync(supplierId);

        if (supplier.HoldType == SupplierHoldType.All)
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
    /// Per ERPNext: hard error, not warning.
    /// </summary>
    public async Task ValidateMinimumOrderQtyAsync(PurchaseOrder order)
    {
        foreach (var poItem in order.Items)
        {
            var item = await _itemRepository.FindAsync(poItem.ItemId);
            if (item != null && item.MinOrderQty > 0 && poItem.Quantity < item.MinOrderQty)
            {
                throw new BusinessException("MyERP:04005")
                    .WithData("itemName", item.ItemName)
                    .WithData("minQty", item.MinOrderQty)
                    .WithData("orderedQty", poItem.Quantity);
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
    /// Validates a Purchase Invoice item does not exceed the PO's pending billing qty.
    /// Prevents over-billing against purchase orders.
    /// </summary>
    public void ValidateBillingQty(PurchaseOrder order, Guid itemId, decimal billingQty)
    {
        var poItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (poItem == null) return;

        if (poItem.BilledQty + billingQty > poItem.Quantity)
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
        IRepository<PurchaseInvoice, Guid> piRepository)
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
    }
}
