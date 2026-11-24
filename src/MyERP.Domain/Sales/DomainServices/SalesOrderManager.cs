using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for Sales Order business rules.
/// Validates cancel guards (dependent docs), close/reopen stock release.
/// Mirrors PurchaseOrderManager for purchasing parity.
/// </summary>
public class SalesOrderManager : DomainService
{
    private readonly IRepository<SalesOrder, Guid> _orderRepository;

    public SalesOrderManager(IRepository<SalesOrder, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    /// <summary>
    /// Validates an SO can be cancelled — blocks if submitted Delivery Notes or Sales Invoices exist.
    /// Per DO-NOT: must cancel children first before parent.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        SalesOrder order,
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<SalesInvoice, Guid> siRepository)
    {
        // Check for submitted Delivery Notes
        var dnQuery = await dnRepository.GetQueryableAsync();
        var hasSubmittedDN = dnQuery.Any(dn =>
            dn.SalesOrderId == order.Id
            && dn.Status != Core.DocumentStatus.Draft
            && dn.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedDN)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Delivery Note");
        }

        // Check for submitted Sales Invoices linked to this SO's items
        var siQuery = await siRepository.GetQueryableAsync();
        var orderItemIds = order.Items.Select(oi => oi.Id).ToList();
        var hasSubmittedSI = siQuery.Any(si =>
            si.Items.Any(i => i.SalesOrderItemId.HasValue && orderItemIds.Contains(i.SalesOrderItemId.Value))
            && si.Status != Core.DocumentStatus.Draft
            && si.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedSI)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Sales Invoice");
        }

        // Per ERPNext check_nextdoc_docstatus(): a DRAFT Sales Invoice against this SO also
        // blocks cancel — separate from the submitted-SI check above. Without this, cancelling
        // the SO would leave a draft SI silently orphaned, still referencing a now-cancelled
        // order via SalesOrderItemId.
        var hasDraftSI = siQuery.Any(si =>
            si.Items.Any(i => i.SalesOrderItemId.HasValue && orderItemIds.Contains(i.SalesOrderItemId.Value))
            && si.Status == Core.DocumentStatus.Draft);

        if (hasDraftSI)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithDraftDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Sales Invoice");
        }
    }

    /// <summary>
    /// Validates over-delivery: DN item qty cannot exceed SO item's allowed qty including tolerance.
    /// Only applies to non-return DNs linked to a Sales Order.
    /// Per ERPNext StatusUpdater: max_allowed = ordered_qty × (1 + allowance_pct / 100),
    /// floored to whole units when the UOM requires whole numbers (PR #60140).
    /// The allowance comes from Company.OverDeliveryReceiptAllowance (Stock Settings in ERPNext).
    /// </summary>
    public void ValidateDeliveryQty(SalesOrder order, Guid itemId, decimal deliveryQty, decimal overDeliveryAllowancePct = 0m, bool mustBeWholeNumber = false)
    {
        var soItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (soItem == null) return;

        // Per ERPNext: max allowed = ordered × (1 + pct/100), then subtract already delivered
        var maxAllowedTotal = soItem.GetMaxDeliverableQty(overDeliveryAllowancePct, mustBeWholeNumber);
        var remainingAllowed = maxAllowedTotal - soItem.DeliveredQty;

        if (deliveryQty > remainingAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverDelivery)
                .WithData("itemName", soItem.Description)
                .WithData("orderedQty", soItem.Quantity)
                .WithData("deliveredQty", soItem.DeliveredQty)
                .WithData("attemptedQty", deliveryQty);
        }
    }

    /// <summary>
    /// Updates SO DeliveredQty and fulfillment status after DN submit/cancel.
    /// </summary>
    public async Task UpdateLinkedOrderDeliveryAsync(
        Guid salesOrderId, Guid itemId, decimal qtyDelta)
    {
        var order = await _orderRepository.GetAsync(salesOrderId);
        var soItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (soItem == null) return;

        soItem.DeliveredQty = Math.Max(0, soItem.DeliveredQty + qtyDelta);
        order.UpdateFulfillmentStatus();
        await _orderRepository.UpdateAsync(order);
    }

    /// <summary>
    /// Checks whether a Sales Order has any line items with billing amount headroom.
    /// Per ERPNext PR #58751 / commit 683f033c34 &amp; commit b099a6aa25:
    /// Closed rows are excluded. A row has headroom if Amount == 0 (e.g. zero-amount service) or
    /// |BilledAmount| &lt; |Amount| * (1 + allowance / 100).
    /// Item-level OverBillingAllowance takes precedence over global allowance when non-zero.
    /// When <paramref name="filterItemId"/> is supplied, only matching Sales Order Item rows are evaluated.
    /// </summary>
    public static bool HasPotentiallyBillableItems(
        SalesOrder so,
        IReadOnlyDictionary<Guid, decimal>? itemOverBillingAllowances = null,
        decimal globalOverBillingAllowance = 0m,
        Guid? filterItemId = null)
    {
        if (so.Status is DocumentStatus.Closed or DocumentStatus.Draft or DocumentStatus.Cancelled)
            return false;

        foreach (var line in so.Items)
        {
            if (line.IsClosed)
                continue;

            if (filterItemId.HasValue && line.ItemId != filterItemId.Value)
                continue;

            decimal allowance = globalOverBillingAllowance;
            if (itemOverBillingAllowances != null &&
                itemOverBillingAllowances.TryGetValue(line.ItemId, out var itemAllowance) &&
                itemAllowance > 0)
            {
                allowance = itemAllowance;
            }

            var itemAmount = line.LineTotal;
            var billedAmount = line.BilledQty * line.UnitPrice;
            // Per ERPNext PR #58816: for zero-amount rows (free items), headroom is based on pending billing quantity
            var hasAmountHeadroom = itemAmount == 0
                ? line.PendingBillingQty > 0
                : Math.Abs(billedAmount) < Math.Abs(itemAmount) * (1m + allowance / 100m);

            // Per ERPNext PR #58966: row must also have pending quantity.
            // When an order is fully billed, allowance headroom alone should not offer it in the invoice picker.
            var hasUnbilledOrderedQty = Math.Round(line.Quantity - line.BilledQty, 4) > 0;
            var hasUnbilledDeliveredQty = Math.Round(line.Quantity - line.ReturnedQty - line.BilledQty, 4) > 0
                || Math.Round(line.DeliveredQty - line.BilledQty, 4) > 0;
            var hasPendingQty = hasUnbilledOrderedQty && hasUnbilledDeliveredQty;

            if (line.Quantity != 0 && hasAmountHeadroom && hasPendingQty)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the Purchase Order <c>ScheduleDate</c> when mapped from a Sales Order row (or Product Bundle row):
    /// falls back to <paramref name="today"/> when the Sales Order's <paramref name="salesOrderDeliveryDate"/>
    /// is null or already in the past (<c>max(deliveryDate ?? today, today)</c>).
    /// Per ERPNext commit <c>3a7d1a09b6</c> (<c>sales_order/mapper.py</c>).
    /// </summary>
    public static DateTime ResolvePurchaseOrderScheduleDateFromSalesOrder(
        DateTime? salesOrderDeliveryDate,
        DateTime today)
    {
        var todayDate = today.Date;
        var candidate = (salesOrderDeliveryDate ?? todayDate).Date;
        return candidate < todayDate ? todayDate : candidate;
    }

    /// <summary>
    /// Validates that a Blanket Order matches the target document's order type and party:
    /// <c>SalesOrder</c> and <c>Quotation</c> both require a <c>Selling</c> Blanket Order for the same customer,
    /// while <c>PurchaseOrder</c> requires a <c>Purchasing</c> Blanket Order for the same supplier.
    /// Returns whether quantity-limit validation should also run (false for <c>Quotation</c>).
    /// Per ERPNext commit <c>138a0e0c0a</c> (<c>blanket_order.py</c> / <c>quotation.py</c>).
    /// </summary>
    public static bool ValidateBlanketOrderMatchesDocument(
        string documentType,
        string blanketOrderType,
        Guid blanketOrderPartyId,
        Guid documentPartyId)
    {
        var isSellingDoc = string.Equals(documentType, "SalesOrder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentType, "Sales Order", StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentType, "Quotation", StringComparison.OrdinalIgnoreCase);

        var expectedBoType = isSellingDoc ? "Selling" : "Purchasing";
        if (!string.Equals(blanketOrderType, expectedBoType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Blanket Order of type '{blanketOrderType}' cannot be used on a '{documentType}'.");
        }

        if (blanketOrderPartyId != documentPartyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Blanket Order party ({blanketOrderPartyId}) does not match document party ({documentPartyId}).");
        }

        var isQuotation = string.Equals(documentType, "Quotation", StringComparison.OrdinalIgnoreCase);
        return !isQuotation;
    }

    /// <summary>
    /// Determines whether a Sales Order status change should cascade to linked Subcontracting Inward Orders
    /// per ERPNext PR #67504 (<c>sales_order/services/status.py</c>):
    /// only closing the Sales Order (<c>"Closed"</c>) closes its inward orders; putting on hold or reopening
    /// leaves inward orders unchanged because users may have closed them intentionally.
    /// </summary>
    public static bool ShouldCloseLinkedSubcontractingInwardOrders(string? targetStatus)
    {
        return string.Equals(targetStatus?.Trim(), "Closed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes line-level <c>GrossProfit</c> for a Sales Order or Quotation row in any UOM
    /// per ERPNext PR #67427 (<c>selling_controller.py: set_item_gross_profit</c>):
    /// computes the stock-UOM selling rate directly from <c>rate * conversionRate / (conversionFactor || 1)</c>
    /// rather than stale <c>stock_uom_rate</c>, then multiplies <c>(stockUomBaseRate - valuationRate) * stockQty</c>.
    /// </summary>
    public static decimal CalculateRowGrossProfit(
        string? documentType,
        decimal itemRate,
        decimal conversionRate,
        decimal conversionFactor,
        decimal valuationRate,
        decimal stockQty,
        int precision = 2)
    {
        var isEligible = string.Equals(documentType, "Sales Order", StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentType, "SalesOrder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentType, "Quotation", StringComparison.OrdinalIgnoreCase);
        if (!isEligible)
        {
            return 0m;
        }

        var effectiveFactor = conversionFactor != 0m ? conversionFactor : 1m;
        var effectiveConvRate = conversionRate != 0m ? conversionRate : 1m;
        var baseStockUomRate = itemRate * effectiveConvRate / effectiveFactor;
        return Math.Round((baseStockUomRate - valuationRate) * stockQty, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Collects the <c>(ItemId, WarehouseId)</c> bin pairs whose <c>ReservedQty</c> must be refreshed
    /// for a Sales Order per ERPNext PR #67706 (<c>sales_order/services/reservation.py</c>):
    /// inspects the Sales Order's own <paramref name="packedItems"/> rows (matched by <c>ParentSalesOrderItemId</c>)
    /// rather than checking whether the Product Bundle master is currently active, so components of a
    /// bundle disabled after order creation still have their reserved qty refreshed.
    /// </summary>
    public static List<(Guid ItemId, Guid WarehouseId)> CollectReservedQtyRefreshPairsForSalesOrder(
        IEnumerable<(Guid RowId, Guid ItemId, Guid? WarehouseId, bool DeliveredBySupplier)> salesOrderItems,
        IEnumerable<(Guid ParentSalesOrderItemId, Guid ItemId, Guid? WarehouseId)> packedItems,
        IReadOnlySet<Guid>? filterSalesOrderItemIds = null)
    {
        var packedByParent = packedItems
            .GroupBy(p => p.ParentSalesOrderItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var pairs = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        foreach (var row in salesOrderItems)
        {
            if (row.DeliveredBySupplier)
                continue;
            if (filterSalesOrderItemIds != null && filterSalesOrderItemIds.Count > 0 && !filterSalesOrderItemIds.Contains(row.RowId))
                continue;

            if (packedByParent.TryGetValue(row.RowId, out var children) && children.Count > 0)
            {
                foreach (var p in children)
                {
                    if (p.ItemId != Guid.Empty && p.WarehouseId.HasValue && p.WarehouseId.Value != Guid.Empty)
                    {
                        pairs.Add((p.ItemId, p.WarehouseId.Value));
                    }
                }
            }
            else if (row.ItemId != Guid.Empty && row.WarehouseId.HasValue && row.WarehouseId.Value != Guid.Empty)
            {
                pairs.Add((row.ItemId, row.WarehouseId.Value));
            }
        }

        return pairs.ToList();
    }

    /// <summary>
    /// Determines whether a Product Bundle parent row's rate should be computed from its child
    /// packed items per ERPNext PR #67702 (<c>packed_item.py: make_packing_list</c>):
    /// skips pricing when <paramref name="parentRowQty"/> is <c>0</c>, as a zero-qty row has no
    /// component amount to price it from.
    /// </summary>
    public static bool ShouldUpdateProductBundleRateFromChildren(
        bool setPriceFromChildren,
        decimal parentRowQty)
        => setPriceFromChildren && parentRowQty != 0m;
}
