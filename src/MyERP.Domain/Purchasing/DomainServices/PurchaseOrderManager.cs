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
    /// Per ERPNext PR #66806 (commits 0e77c8d15e & 3af55d53f6): aggregates split PO lines per MR item
    /// before converting and snaps floating-point remainders within 4-decimal rounding.
    /// </summary>
    public async Task UpdateMaterialRequestOrderedQtyAsync(PurchaseOrder order, bool reverse = false)
    {
        var mrItemIds = order.Items
            .Where(i => i.MaterialRequestItemId.HasValue)
            .Select(i => i.MaterialRequestItemId!.Value)
            .Distinct()
            .ToList();

        if (!mrItemIds.Any()) return;

        var mrQuery = await _mrRepository.GetQueryableAsync();
        var affectedMRs = mrQuery
            .Where(mr => mr.Items.Any(i => mrItemIds.Contains(i.Id)))
            .ToList();

        var groupedByMrItem = order.Items
            .Where(i => i.MaterialRequestItemId.HasValue)
            .GroupBy(i => i.MaterialRequestItemId!.Value)
            .ToList();

        foreach (var mr in affectedMRs)
        {
            var touched = false;
            foreach (var group in groupedByMrItem)
            {
                var mrItem = mr.Items.FirstOrDefault(i => i.Id == group.Key);
                if (mrItem != null)
                {
                    var mrFactor = mrItem.ConversionFactor > 0 ? mrItem.ConversionFactor : 1m;
                    var allSameUom = group.All(poItem =>
                        (poItem.ConversionFactor > 0 ? poItem.ConversionFactor : 1m) == mrFactor);
                    var qtyInMrUom = allSameUom
                        ? group.Sum(poItem => poItem.Quantity)
                        : group.Sum(poItem => poItem.Quantity * (poItem.ConversionFactor > 0 ? poItem.ConversionFactor : 1m)) / mrFactor;
                    var delta = reverse ? -qtyInMrUom : qtyInMrUom;
                    var updated = Math.Max(0m, mrItem.OrderedQuantity + delta);
                    if (Math.Round(updated - mrItem.Quantity, 4) == 0m)
                    {
                        updated = Math.Round(mrItem.Quantity, 4);
                    }
                    else if (Math.Round(updated, 4) == 0m)
                    {
                        updated = 0m;
                    }

                    mrItem.OrderedQuantity = updated;
                    touched = true;
                }
            }

            if (touched)
            {
                mr.UpdateFulfillmentStatus();
                await _mrRepository.UpdateAsync(mr);
            }
        }
    }

    /// <summary>
    /// Updates Sales Order items' OrderedQty (stock UOM) when a PO created from a Sales Order is
    /// submitted or cancelled. Per ERPNext Purchase Order Item → Sales Order Item status updater
    /// (source stock_qty → target ordered_qty, PR #66806 commits 21d02fa8bf & 08175848a3). Reverse never drops below zero.
    /// </summary>
    public async Task UpdateSalesOrderOrderedQtyAsync(
        PurchaseOrder order,
        IRepository<MyERP.Sales.Entities.SalesOrder, Guid> soRepository,
        bool reverse = false)
    {
        var soItemIds = order.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (!soItemIds.Any()) return;

        var soQuery = await soRepository.GetQueryableAsync();
        var affectedSOs = soQuery
            .Where(so => so.Items.Any(i => soItemIds.Contains(i.Id)))
            .ToList();

        var groupedBySoItem = order.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .GroupBy(i => i.SalesOrderItemId!.Value)
            .ToList();

        foreach (var so in affectedSOs)
        {
            foreach (var group in groupedBySoItem)
            {
                var soItem = so.Items.FirstOrDefault(i => i.Id == group.Key);
                if (soItem == null) continue;

                var totalStockQty = group.Sum(poItem => poItem.Quantity * (poItem.ConversionFactor > 0 ? poItem.ConversionFactor : 1m));
                var delta = reverse ? -totalStockQty : totalStockQty;
                var updated = Math.Max(0m, soItem.OrderedQty + delta);
                if (Math.Round(updated - soItem.StockQty, 4) == 0m)
                {
                    updated = Math.Round(soItem.StockQty, 4);
                }
                else if (Math.Round(updated, 4) == 0m)
                {
                    updated = 0m;
                }

                soItem.OrderedQty = updated;
            }
            await soRepository.UpdateAsync(so);
        }
    }

    /// <summary>
    /// Updates Supplier Quotation items' OrderedQty and status when PO is submitted or cancelled.
    /// Per ERPNext PR #58572 & PR #66806: tracks supplier quotation fulfillment and transitions status.
    /// </summary>
    public async Task UpdateSupplierQuotationOrderedQtyAsync(
        PurchaseOrder order,
        IRepository<SupplierQuotation, Guid> sqRepository,
        bool reverse = false)
    {
        var sqItemIds = order.Items
            .Where(i => i.SupplierQuotationItemId.HasValue)
            .Select(i => i.SupplierQuotationItemId!.Value)
            .Distinct()
            .ToList();

        if (!sqItemIds.Any() && !order.SupplierQuotationId.HasValue) return;

        var sqQuery = await sqRepository.GetQueryableAsync();
        var affectedSQs = sqQuery
            .Where(sq => (order.SupplierQuotationId.HasValue && sq.Id == order.SupplierQuotationId.Value)
                      || sq.Items.Any(i => sqItemIds.Contains(i.Id)))
            .ToList();

        foreach (var sq in affectedSQs)
        {
            if (!reverse && sq.Status == DocumentStatus.Cancelled)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("reason", $"Supplier Quotation {sq.QuotationNumber ?? sq.Id.ToString()} is cancelled.");
            }

            if (!reverse && sq.Status == DocumentStatus.Draft)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("reason", $"Supplier Quotation {sq.QuotationNumber ?? sq.Id.ToString()} is in Draft status.");
            }

            var matchedBySqItem = new Dictionary<Guid, decimal>();
            foreach (var poItem in order.Items)
            {
                SupplierQuotationItem? sqItem = null;
                if (poItem.SupplierQuotationItemId.HasValue)
                {
                    sqItem = sq.Items.FirstOrDefault(i => i.Id == poItem.SupplierQuotationItemId.Value);
                }
                else if (order.SupplierQuotationId == sq.Id)
                {
                    sqItem = sq.Items.FirstOrDefault(i => i.ItemId == poItem.ItemId);
                }

                if (sqItem != null)
                {
                    var itemStockQty = poItem.Quantity * (poItem.ConversionFactor > 0 ? poItem.ConversionFactor : 1m);
                    matchedBySqItem[sqItem.Id] = matchedBySqItem.GetValueOrDefault(sqItem.Id) + itemStockQty;
                }
            }

            foreach (var (sqItemId, totalStockQty) in matchedBySqItem)
            {
                var delta = reverse ? -totalStockQty : totalStockQty;
                sq.UpdateOrderedQty(sqItemId, delta);
            }
            await sqRepository.UpdateAsync(sq);
        }
    }

    /// <summary>
    /// Adjusts Material Request items' OrderedQuantity and fulfillment status when PO items
    /// are updated or removed via Update Items. Converts deltas into MR UOM.
    /// </summary>
    public async Task AdjustMaterialRequestOrderedQtyAsync(
        IReadOnlyList<(Guid MaterialRequestItemId, decimal OldQuantity, decimal NewQuantity, decimal OldConversionFactor, decimal NewConversionFactor)> deltas)
    {
        if (deltas.Count == 0) return;

        var mrItemIds = deltas.Select(d => d.MaterialRequestItemId).Distinct().ToList();
        var mrQuery = await _mrRepository.WithDetailsAsync(mr => mr.Items)
            ?? await _mrRepository.GetQueryableAsync();
        var affectedMRs = mrQuery
            .Where(mr => mr.Items.Any(i => mrItemIds.Contains(i.Id)))
            .ToList();

        var groupedDeltas = deltas.GroupBy(d => d.MaterialRequestItemId).ToList();

        foreach (var mr in affectedMRs)
        {
            var touched = false;
            foreach (var group in groupedDeltas)
            {
                var mrItem = mr.Items.FirstOrDefault(i => i.Id == group.Key);
                if (mrItem == null) continue;

                var mrFactor = mrItem.ConversionFactor > 0m ? mrItem.ConversionFactor : 1m;
                var totalDeltaInMrUom = 0m;
                foreach (var d in group)
                {
                    var oldCf = d.OldConversionFactor > 0m ? d.OldConversionFactor : 1m;
                    var newCf = d.NewConversionFactor > 0m ? d.NewConversionFactor : 1m;

                    totalDeltaInMrUom += (oldCf == mrFactor && newCf == mrFactor)
                        ? (d.NewQuantity - d.OldQuantity)
                        : ((d.NewQuantity * newCf) - (d.OldQuantity * oldCf)) / mrFactor;
                }

                if (totalDeltaInMrUom == 0m) continue;

                var updated = Math.Max(0m, mrItem.OrderedQuantity + totalDeltaInMrUom);
                if (Math.Round(updated - mrItem.Quantity, 4) == 0m)
                {
                    updated = Math.Round(mrItem.Quantity, 4);
                }
                else if (Math.Round(updated, 4) == 0m)
                {
                    updated = 0m;
                }

                mrItem.OrderedQuantity = updated;
                touched = true;
            }

            if (touched)
            {
                mr.UpdateFulfillmentStatus();
                await _mrRepository.UpdateAsync(mr);
            }
        }
    }

    /// <summary>
    /// Adjusts Sales Order items' OrderedQty (in stock UOM) when linked PO items are
    /// updated or removed via Update Items (per ERPNext commits cf91622391 & 08175848a3).
    /// </summary>
    public async Task AdjustSalesOrderOrderedQtyAsync(
        IReadOnlyList<(Guid SalesOrderItemId, decimal DeltaStockQty)> deltas,
        IRepository<MyERP.Sales.Entities.SalesOrder, Guid> soRepository)
    {
        var nonZero = deltas.Where(d => d.DeltaStockQty != 0m).ToList();
        if (nonZero.Count == 0) return;

        var soItemIds = nonZero.Select(d => d.SalesOrderItemId).Distinct().ToList();
        var soQuery = await soRepository.WithDetailsAsync(so => so.Items)
            ?? await soRepository.GetQueryableAsync();
        var affectedSOs = soQuery
            .Where(so => so.Items.Any(i => soItemIds.Contains(i.Id)))
            .ToList();

        var groupedBySoItem = nonZero
            .GroupBy(d => d.SalesOrderItemId)
            .Select(g => (SalesOrderItemId: g.Key, TotalDeltaStockQty: g.Sum(x => x.DeltaStockQty)))
            .ToList();

        foreach (var so in affectedSOs)
        {
            var touched = false;
            foreach (var d in groupedBySoItem)
            {
                var soItem = so.Items.FirstOrDefault(i => i.Id == d.SalesOrderItemId);
                if (soItem == null) continue;

                var updated = Math.Max(0m, soItem.OrderedQty + d.TotalDeltaStockQty);
                if (Math.Round(updated - soItem.StockQty, 4) == 0m)
                {
                    updated = Math.Round(soItem.StockQty, 4);
                }
                else if (Math.Round(updated, 4) == 0m)
                {
                    updated = 0m;
                }

                soItem.OrderedQty = updated;
                touched = true;
            }

            if (touched)
            {
                await soRepository.UpdateAsync(so);
            }
        }
    }

    /// <summary>
    /// Adjusts Supplier Quotation items' OrderedQty and fulfillment status when linked PO items
    /// are updated or removed via Update Items.
    /// </summary>
    public async Task AdjustSupplierQuotationOrderedQtyAsync(
        Guid? orderSupplierQuotationId,
        IReadOnlyList<(Guid? SupplierQuotationItemId, Guid ItemId, decimal DeltaStockQty)> deltas,
        IRepository<SupplierQuotation, Guid> sqRepository)
    {
        var relevant = deltas
            .Where(d => d.DeltaStockQty != 0m && (d.SupplierQuotationItemId.HasValue || orderSupplierQuotationId.HasValue))
            .ToList();
        if (relevant.Count == 0) return;

        var sqItemIds = relevant
            .Where(d => d.SupplierQuotationItemId.HasValue)
            .Select(d => d.SupplierQuotationItemId!.Value)
            .Distinct()
            .ToList();

        var sqQuery = await sqRepository.WithDetailsAsync(sq => sq.Items)
            ?? await sqRepository.GetQueryableAsync();
        var affectedSQs = sqQuery
            .Where(sq => (orderSupplierQuotationId.HasValue && sq.Id == orderSupplierQuotationId.Value)
                      || sq.Items.Any(i => sqItemIds.Contains(i.Id)))
            .ToList();

        foreach (var sq in affectedSQs)
        {
            var matchedDeltas = new Dictionary<Guid, decimal>();
            foreach (var d in relevant)
            {
                SupplierQuotationItem? sqItem = null;
                if (d.SupplierQuotationItemId.HasValue)
                {
                    sqItem = sq.Items.FirstOrDefault(i => i.Id == d.SupplierQuotationItemId.Value);
                }
                else if (orderSupplierQuotationId == sq.Id)
                {
                    sqItem = sq.Items.FirstOrDefault(i => i.ItemId == d.ItemId);
                }

                if (sqItem != null)
                {
                    matchedDeltas[sqItem.Id] = matchedDeltas.GetValueOrDefault(sqItem.Id) + d.DeltaStockQty;
                }
            }

            if (matchedDeltas.Count > 0)
            {
                foreach (var (sqItemId, totalDelta) in matchedDeltas)
                {
                    sq.UpdateOrderedQty(sqItemId, totalDelta);
                }
                await sqRepository.UpdateAsync(sq);
            }
        }
    }

    /// <summary>
    /// Validates a Purchase Receipt item does not exceed the PO's allowed receipt qty including tolerance.
    /// Per ERPNext: max_allowed = ordered_qty × (1 + allowance_pct / 100) - already_received.
    /// The allowance comes from Company.OverDeliveryReceiptAllowance (Stock Settings in ERPNext).
    /// </summary>
    public void ValidateReceiptQty(PurchaseOrder order, Guid itemId, decimal receiptQty, decimal overReceiptAllowancePct = 0m, bool mustBeWholeNumber = false)
    {
        var poItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (poItem == null) return;

        var maxAllowedTotal = poItem.GetMaxReceivableQty(overReceiptAllowancePct, mustBeWholeNumber);
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
    /// including the company's over-billing tolerance percentage and UOM conversion.
    /// Per ERPNext PR #61336 / PR #58966: max_allowed = ordered_qty × (1 + allowance_pct / 100).
    /// </summary>
    public void ValidateBillingQty(PurchaseOrder order, Guid itemId, decimal billingQty, decimal overBillingAllowancePct = 0m, decimal conversionFactor = 1m)
    {
        var poItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (poItem == null) return;

        var effectiveBillingQty = (poItem.ConversionFactor > 0 && conversionFactor > 0)
            ? Math.Round(billingQty * conversionFactor / poItem.ConversionFactor, 4)
            : billingQty;

        var maxAllowedTotal = poItem.Quantity * (1m + overBillingAllowancePct / 100m);
        if (poItem.BilledQty + effectiveBillingQty > maxAllowedTotal)
        {
            throw new BusinessException("MyERP:08007")
                .WithData("itemName", poItem.Description)
                .WithData("orderedQty", poItem.Quantity)
                .WithData("billedQty", poItem.BilledQty)
                .WithData("attemptedQty", effectiveBillingQty);
        }
    }

    /// <summary>
    /// Validates multiple billing lines against Purchase Order rows, summing stock qty per PO item before converting.
    /// Per ERPNext PR #61336 (commits 1b35280c46 & b97eb00c19):
    /// sum converted qty across split rows before checking overbilling tolerance.
    /// </summary>
    public void ValidateBillingQty(
        PurchaseOrder order,
        IEnumerable<(Guid PurchaseOrderItemId, decimal Quantity, decimal ConversionFactor)> billingLines,
        decimal overBillingAllowancePct = 0m)
    {
        var grouped = billingLines.GroupBy(l => l.PurchaseOrderItemId);
        foreach (var group in grouped)
        {
            var poItem = order.Items.FirstOrDefault(i => i.Id == group.Key);
            if (poItem == null) continue;

            var totalAttemptedStockQty = group.Sum(l => l.Quantity * (l.ConversionFactor > 0 ? l.ConversionFactor : 1m));
            var attemptedInPoUom = poItem.ConversionFactor > 0
                ? Math.Round(totalAttemptedStockQty / poItem.ConversionFactor, 4)
                : totalAttemptedStockQty;

            var maxAllowedTotal = poItem.Quantity * (1m + overBillingAllowancePct / 100m);
            if (poItem.BilledQty + attemptedInPoUom > maxAllowedTotal)
            {
                throw new BusinessException("MyERP:08007")
                    .WithData("itemName", poItem.Description)
                    .WithData("orderedQty", poItem.Quantity)
                    .WithData("billedQty", poItem.BilledQty)
                    .WithData("attemptedQty", attemptedInPoUom);
            }
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

    /// <summary>
    /// Validates that ordered quantity meets minimum order quantity defined on the Item.
    /// Per ERPNext PR #57859 / commit 98b7407949: compares at stock_qty precision to tolerate UOM conversion dust.
    /// </summary>
    public async Task ValidateMinOrderQtyAsync(PurchaseOrder order)
    {
        var itemIds = order.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.MinOrderQty, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id);

        var itemwiseQty = order.Items
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.StockQty));

        foreach (var (itemId, stockQty) in itemwiseQty)
        {
            if (items.TryGetValue(itemId, out var item) && item.MinOrderQty > 0)
            {
                var roundedStockQty = Math.Round(stockQty, 4);
                var roundedMinOrderQty = Math.Round(item.MinOrderQty, 4);
                if (roundedStockQty < roundedMinOrderQty)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Item {item.ItemCode}: Ordered qty {roundedStockQty} cannot be less than minimum order qty {roundedMinOrderQty}.");
                }
            }
        }
    }

    /// <summary>
    /// Validates that items are not duplicated when AllowMultipleItems is disabled in Buying Settings.
    /// Per ERPNext buying/utils.py validate_duplicate_items.
    /// </summary>
    public void ValidateDuplicateItems(PurchaseOrder order, bool allowMultipleItems)
    {
        if (allowMultipleItems)
            return;

        var duplicates = order.Items
            .GroupBy(i => i.ItemId)
            .Any(g => g.Count() > 1);

        if (duplicates)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Same item cannot be entered multiple times.");
        }
    }

    /// <summary>
    /// Converts drop-ship received quantity from Purchase Order UOM to Sales Order UOM via stock UOM.
    /// Per ERPNext PR #59832 (commit 008fb0c12e): get_drop_ship_delivered_qty.
    /// </summary>
    public static decimal CalculateDropShipDeliveredQty(
        decimal poReceivedQty,
        decimal poConversionFactor,
        decimal soConversionFactor)
    {
        var effectivePoFactor = poConversionFactor > 0 ? poConversionFactor : 1m;
        var effectiveSoFactor = soConversionFactor > 0 ? soConversionFactor : 1m;
        var receivedStockQty = poReceivedQty * effectivePoFactor;
        return Math.Round(receivedStockQty / effectiveSoFactor, 4);
    }

    /// <summary>
    /// Determines whether a Purchase Order Item row is subject to quantity over-receipt limits
    /// per ERPNext PR #67402 (commit c5f5a53ad6 in <c>status_updater.py</c>):
    /// stock items, active Product Bundles, AND non-stock subcontracted service rows (those linked to a
    /// finished good <paramref name="fgItemId"/>) are all capped by their ordered qty + allowance.
    /// </summary>
    public static bool IsQuantityLimitEnforcedForPurchaseOrderItem(
        bool isStockItem,
        bool isActiveProductBundle,
        Guid? fgItemId)
    {
        return isStockItem || isActiveProductBundle || (fgItemId.HasValue && fgItemId.Value != Guid.Empty);
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
