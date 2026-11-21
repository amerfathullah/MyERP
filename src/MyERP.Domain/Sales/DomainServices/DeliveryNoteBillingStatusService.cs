using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Computes and synchronizes settled return quantities on Return Delivery Notes
/// per ERPNext PR #60607 (erpnext/stock/doctype/delivery_note/services/billing_status.py).
///
/// When a Delivery Note return is deducted on the original Delivery Note's invoice (or the entire
/// unbilled Delivery Note is returned), no separate Credit Note is due — those return rows are
/// marked settled so the Return Delivery Note moves to Completed (100% PerBilled).
/// Over-billing directly against a linked Sales Order reduces settleable return qty, shared
/// chronologically across sibling Delivery Notes.
/// </summary>
public class DeliveryNoteBillingStatusService : DomainService
{
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<SalesInvoice, Guid> _siRepository;

    public DeliveryNoteBillingStatusService(
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<SalesInvoice, Guid> siRepository)
    {
        _dnRepository = dnRepository;
        _siRepository = siRepository;
    }

    private static bool IsSubmittedOrPosted(DocumentStatus status) =>
        status is DocumentStatus.Submitted or DocumentStatus.Posted;

    /// <summary>
    /// Computes the settled return quantity (in each return row's transaction UOM)
    /// for all submitted Return Delivery Notes referencing <paramref name="originalDeliveryNoteId"/>.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetSettledReturnQtyAsync(Guid originalDeliveryNoteId)
    {
        var dnQuery = (await _dnRepository.WithDetailsAsync()).ToList();
        var originalDn = dnQuery.FirstOrDefault(d => d.Id == originalDeliveryNoteId);
        if (originalDn == null || originalDn.IsReturn || !IsSubmittedOrPosted(originalDn.Status))
        {
            return new Dictionary<Guid, decimal>();
        }

        var returnDns = dnQuery
            .Where(d => d.IsReturn
                && d.ReturnAgainstId == originalDeliveryNoteId
                && IsSubmittedOrPosted(d.Status))
            .OrderBy(d => d.PostingDate)
            .ThenBy(d => d.CreationTime)
            .ThenBy(d => d.Id)
            .ToList();

        if (returnDns.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var siQuery = (await _siRepository.WithDetailsAsync()).ToList();
        var soItemIds = originalDn.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .ToHashSet();

        var overbilledByOrigItemId = ComputeOverbilledQtyOfDeliveryRows(soItemIds, dnQuery, siQuery);

        var origById = originalDn.Items.ToDictionary(i => i.Id);
        var mappedReturnRows = new List<(DeliveryNoteItem ReturnItem, DeliveryNoteItem OrigItem, decimal ToOrigFactor, decimal OrigQty)>();
        var totalReturnedByOrigId = new Dictionary<Guid, decimal>();

        foreach (var retDn in returnDns)
        {
            foreach (var retItem in retDn.Items)
            {
                var origItem = MatchOriginalItem(originalDn, retItem, origById);
                if (origItem == null)
                {
                    continue;
                }

                var retFactor = retItem.ConversionFactor > 0 ? retItem.ConversionFactor : 1m;
                var origFactor = origItem.ConversionFactor > 0 ? origItem.ConversionFactor : 1m;
                var toOrigFactor = retFactor / origFactor;
                var qtyInOrigUom = Math.Abs(retItem.Quantity) * toOrigFactor;

                mappedReturnRows.Add((retItem, origItem, toOrigFactor, qtyInOrigUom));
                totalReturnedByOrigId[origItem.Id] = totalReturnedByOrigId.GetValueOrDefault(origItem.Id, 0m) + qtyInOrigUom;
            }
        }

        if (mappedReturnRows.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var remainingSettleableByOrigId = new Dictionary<Guid, decimal>();
        foreach (var (origId, rawReturned) in totalReturnedByOrigId)
        {
            var origItem = origById[origId];
            var returned = Math.Round(rawReturned, 4, MidpointRounding.AwayFromZero);
            var netInvoiced = Math.Round(Math.Abs(origItem.BilledQty), 4, MidpointRounding.AwayFromZero);
            var uninvoiced = Math.Round(Math.Abs(origItem.Quantity) - netInvoiced, 4, MidpointRounding.AwayFromZero);

            // Until net qty is fully invoiced, subsequent invoices still deduct the returns.
            var fullyInvoiced = returned >= uninvoiced;
            var settleable = fullyInvoiced ? Math.Max(Math.Min(returned, uninvoiced), 0m) : 0m;
            var overbilled = overbilledByOrigItemId.GetValueOrDefault(origId, 0m);
            remainingSettleableByOrigId[origId] = Math.Max(Math.Round(settleable - overbilled, 4, MidpointRounding.AwayFromZero), 0m);
        }

        var settledByReturnItemId = new Dictionary<Guid, decimal>();
        foreach (var (retItem, origItem, toOrigFactor, qtyInOrigUom) in mappedReturnRows)
        {
            var available = remainingSettleableByOrigId.GetValueOrDefault(origItem.Id, 0m);
            var settledInOrig = Math.Min(qtyInOrigUom, available);
            remainingSettleableByOrigId[origItem.Id] = Math.Max(0m, available - settledInOrig);
            var settledInReturnUom = toOrigFactor > 0
                ? Math.Round(settledInOrig / toOrigFactor, 4, MidpointRounding.AwayFromZero)
                : 0m;
            settledByReturnItemId[retItem.Id] = settledInReturnUom;
        }

        return settledByReturnItemId;
    }

    /// <summary>
    /// Recalculates and persists <see cref="DeliveryNoteItem.SettledQty"/> on all submitted Return
    /// Delivery Notes referencing <paramref name="originalDeliveryNoteId"/>.
    /// </summary>
    public async Task RecalculateSettledReturnsAsync(Guid originalDeliveryNoteId)
    {
        var settledMap = await GetSettledReturnQtyAsync(originalDeliveryNoteId);
        var returnDns = (await _dnRepository.WithDetailsAsync())
            .Where(d => d.IsReturn
                && d.ReturnAgainstId == originalDeliveryNoteId
                && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.Posted))
            .ToList();

        foreach (var retDn in returnDns)
        {
            var changed = false;
            foreach (var item in retDn.Items)
            {
                var targetSettled = settledMap.GetValueOrDefault(item.Id, 0m);
                if (item.SettledQty != targetSettled)
                {
                    item.SettledQty = targetSettled;
                    changed = true;
                }
            }

            if (changed)
            {
                retDn.UpdateBillingStatus();
                await _dnRepository.UpdateAsync(retDn, autoSave: true);
            }
        }
    }

    /// <summary>
    /// Re-evaluates settled return status for all Delivery Notes whose rows reference any of
    /// <paramref name="salesOrderItemIds"/> (needed when direct SO invoices or sibling DNs change).
    /// </summary>
    public async Task RecalculateSettledReturnsForSalesOrderItemsAsync(IEnumerable<Guid> salesOrderItemIds)
    {
        var soItemIdSet = salesOrderItemIds.Where(id => id != Guid.Empty).ToHashSet();
        if (soItemIdSet.Count == 0)
        {
            return;
        }

        var affectedOrigDnIds = (await _dnRepository.WithDetailsAsync())
            .Where(d => !d.IsReturn
                && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.Posted)
                && d.Items.Any(i => i.SalesOrderItemId.HasValue && soItemIdSet.Contains(i.SalesOrderItemId.Value)))
            .Select(d => d.Id)
            .Distinct()
            .ToList();

        foreach (var dnId in affectedOrigDnIds)
        {
            await RecalculateSettledReturnsAsync(dnId);
        }
    }

    private static DeliveryNoteItem? MatchOriginalItem(
        DeliveryNote originalDn,
        DeliveryNoteItem returnItem,
        IReadOnlyDictionary<Guid, DeliveryNoteItem> origById)
    {
        if (returnItem.OriginalDeliveryNoteItemId.HasValue
            && origById.TryGetValue(returnItem.OriginalDeliveryNoteItemId.Value, out var direct))
        {
            return direct;
        }

        return originalDn.Items.FirstOrDefault(i =>
            (returnItem.SalesOrderItemId.HasValue && i.SalesOrderItemId == returnItem.SalesOrderItemId)
            || (!returnItem.SalesOrderItemId.HasValue && i.ItemId == returnItem.ItemId));
    }

    private static Dictionary<Guid, decimal> ComputeOverbilledQtyOfDeliveryRows(
        HashSet<Guid> soItemIds,
        List<DeliveryNote> allDns,
        List<SalesInvoice> allSis)
    {
        if (soItemIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var submittedNonReturnNonStockSiItems = allSis
            .Where(si => IsSubmittedOrPosted(si.Status) && !si.IsReturn && !si.UpdateStock)
            .SelectMany(si => si.Items)
            .Where(i => i.SalesOrderItemId.HasValue && soItemIds.Contains(i.SalesOrderItemId.Value))
            .ToList();

        var submittedDnItemsForSo = allDns
            .Where(d => IsSubmittedOrPosted(d.Status))
            .SelectMany(d => d.Items.Select(item => new { Dn = d, Item = item }))
            .Where(x => x.Item.SalesOrderItemId.HasValue && soItemIds.Contains(x.Item.SalesOrderItemId.Value))
            .ToList();

        var soOverbilledRemaining = new Dictionary<Guid, decimal>();
        foreach (var soItemId in soItemIds)
        {
            var billedStockQty = submittedNonReturnNonStockSiItems
                .Where(i => i.SalesOrderItemId == soItemId)
                .Sum(i => i.StockQty);

            var billedAgainstSoDirectlyStockQty = submittedNonReturnNonStockSiItems
                .Where(i => i.SalesOrderItemId == soItemId && !i.DeliveryNoteItemId.HasValue)
                .Sum(i => i.StockQty);

            // Signed StockQty sums positive deliveries and negative returns to yield net delivered stock.
            var netDeliveredStockQty = submittedDnItemsForSo
                .Where(x => x.Item.SalesOrderItemId == soItemId)
                .Sum(x => x.Item.StockQty);

            var overbilled = Math.Round(billedStockQty - netDeliveredStockQty, 4, MidpointRounding.AwayFromZero);
            soOverbilledRemaining[soItemId] = Math.Max(Math.Min(overbilled, billedAgainstSoDirectlyStockQty), 0m);
        }

        if (!soOverbilledRemaining.Values.Any(v => v > 0m))
        {
            return new Dictionary<Guid, decimal>();
        }

        var deliveryRows = allDns
            .Where(d => !d.IsReturn && IsSubmittedOrPosted(d.Status))
            .OrderBy(d => d.PostingDate)
            .ThenBy(d => d.CreationTime)
            .ThenBy(d => d.Id)
            .SelectMany(d => d.Items.OrderBy(i => i.Idx).Select(item => new { Dn = d, Item = item }))
            .Where(x => x.Item.SalesOrderItemId.HasValue && soItemIds.Contains(x.Item.SalesOrderItemId.Value))
            .ToList();

        var overbilledInOrigUom = new Dictionary<Guid, decimal>();
        foreach (var row in deliveryRows)
        {
            var soItemId = row.Item.SalesOrderItemId!.Value;
            var factor = row.Item.ConversionFactor > 0 ? row.Item.ConversionFactor : 1m;
            var returnedStock = Math.Abs(row.Item.ReturnedQty) * factor;
            var uninvoicedStock = (Math.Abs(row.Item.Quantity) - Math.Abs(row.Item.BilledQty)) * factor;
            var uninvoicedReturned = Math.Max(Math.Min(returnedStock, uninvoicedStock), 0m);

            var share = Math.Min(uninvoicedReturned, soOverbilledRemaining.GetValueOrDefault(soItemId, 0m));
            soOverbilledRemaining[soItemId] = Math.Max(0m, soOverbilledRemaining.GetValueOrDefault(soItemId, 0m) - share);
            overbilledInOrigUom[row.Item.Id] = Math.Round(share / factor, 4, MidpointRounding.AwayFromZero);
        }

        return overbilledInOrigUom;
    }

    /// <summary>
    /// Computes settled return quantities across Return Delivery Note rows in FIFO order when a Sales Invoice
    /// made either directly from the Delivery Note OR from its linked Sales Order deducts returned quantities,
    /// batching direct-invoice quantity lookups across all delivery rows
    /// per ERPNext commits d8f23e2c4f and 90702b5888 (<c>billing_status.py</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateSettledReturnQtyFromDirectAndSalesOrderInvoices(
        IEnumerable<(Guid OrigDnItemId, decimal OrigQty, decimal DirectInvoicedQtyInOrigUom, decimal ConversionFactor, decimal SoOverbilledShareInOrigUom)> originalDeliveryRows,
        IEnumerable<(Guid ReturnDnItemId, Guid OrigDnItemId, decimal ReturnQty, decimal ConversionFactor)> chronologicalReturnRows)
    {
        var result = new Dictionary<Guid, decimal>();
        if (originalDeliveryRows == null || chronologicalReturnRows == null)
        {
            return result;
        }

        var origById = originalDeliveryRows.ToDictionary(r => r.OrigDnItemId);
        var returnList = chronologicalReturnRows.ToList();
        var totalReturnedInOrigUom = new Dictionary<Guid, decimal>();

        foreach (var ret in returnList)
        {
            if (!origById.TryGetValue(ret.OrigDnItemId, out var orig))
            {
                continue;
            }

            var retFactor = ret.ConversionFactor > 0m ? ret.ConversionFactor : 1m;
            var origFactor = orig.ConversionFactor > 0m ? orig.ConversionFactor : 1m;
            var toOrigFactor = retFactor / origFactor;
            var qtyInOrig = Math.Abs(ret.ReturnQty) * toOrigFactor;
            totalReturnedInOrigUom[ret.OrigDnItemId] = totalReturnedInOrigUom.GetValueOrDefault(ret.OrigDnItemId, 0m) + qtyInOrig;
        }

        var remainingSettleableByOrig = new Dictionary<Guid, decimal>();
        foreach (var (origId, rawReturned) in totalReturnedInOrigUom)
        {
            var orig = origById[origId];
            var returned = Math.Round(rawReturned, 4, MidpointRounding.AwayFromZero);
            var netInvoiced = Math.Round(Math.Abs(orig.DirectInvoicedQtyInOrigUom), 4, MidpointRounding.AwayFromZero);
            var uninvoiced = Math.Round(Math.Abs(orig.OrigQty) - netInvoiced, 4, MidpointRounding.AwayFromZero);

            // Both direct DN invoicing and SO invoicing (after deducting returned qty) leave uninvoiced <= returned
            var fullyInvoiced = returned >= uninvoiced;
            var settleable = fullyInvoiced ? Math.Max(Math.Min(returned, uninvoiced), 0m) : 0m;
            var afterOverbilled = Math.Max(Math.Round(settleable - Math.Max(orig.SoOverbilledShareInOrigUom, 0m), 4, MidpointRounding.AwayFromZero), 0m);
            remainingSettleableByOrig[origId] = afterOverbilled;
        }

        foreach (var ret in returnList)
        {
            if (!origById.TryGetValue(ret.OrigDnItemId, out var orig))
            {
                continue;
            }

            var retFactor = ret.ConversionFactor > 0m ? ret.ConversionFactor : 1m;
            var origFactor = orig.ConversionFactor > 0m ? orig.ConversionFactor : 1m;
            var toOrigFactor = retFactor / origFactor;
            var qtyInOrig = Math.Abs(ret.ReturnQty) * toOrigFactor;

            var avail = remainingSettleableByOrig.GetValueOrDefault(ret.OrigDnItemId, 0m);
            var settledOrig = Math.Min(qtyInOrig, avail);
            remainingSettleableByOrig[ret.OrigDnItemId] = Math.Max(0m, avail - settledOrig);
            result[ret.ReturnDnItemId] = toOrigFactor > 0m
                ? Math.Round(settledOrig / toOrigFactor, 4, MidpointRounding.AwayFromZero)
                : 0m;
        }

        return result;
    }
}
