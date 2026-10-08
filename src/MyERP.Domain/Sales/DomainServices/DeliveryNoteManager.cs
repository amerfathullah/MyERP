using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for Delivery Note business rules.
/// Validates return documents, over-delivery against SO, and cancel guards.
/// Mirrors PurchaseReceiptManager for purchasing parity.
/// </summary>
public class DeliveryNoteManager : DomainService
{
    private readonly IRepository<DeliveryNote, Guid> _dnRepository;
    private readonly IRepository<SalesOrder, Guid> _orderRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Uom, Guid>? _uomRepository;

    public DeliveryNoteManager(
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<SalesOrder, Guid> orderRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Uom, Guid>? uomRepository = null)
    {
        _dnRepository = dnRepository;
        _orderRepository = orderRepository;
        _companyRepository = companyRepository;
        _itemRepository = itemRepository;
        _uomRepository = uomRepository;
    }

    /// <summary>
    /// Validates receipt quantities against the linked Sales Order.
    /// Prevents over-delivery: each DN item qty must not exceed SO item's allowed qty,
    /// including the company's over-delivery tolerance percentage, floored for whole number UOMs (PR #60140).
    /// Per ERPNext StatusUpdater: max_allowed = ordered_qty × (1 + allowance_pct / 100).
    /// Only applies to non-return DNs linked to a SO.
    /// </summary>
    public async Task ValidateAgainstSalesOrderAsync(DeliveryNote dn)
    {
        if (dn.IsReturn || !dn.SalesOrderId.HasValue) return;

        var so = await _orderRepository.GetAsync(dn.SalesOrderId.Value);

        // SO must be in an active fulfillment state
        if (so.Status == Core.DocumentStatus.Cancelled || so.Status == Core.DocumentStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Sales Order")
                .WithData("status", so.Status.ToString());
        }

        var company = await _companyRepository.GetAsync(dn.CompanyId);
        var allowancePct = company.OverDeliveryReceiptAllowance;

        var uomRepo = _uomRepository ?? LazyServiceProvider.LazyGetService<IRepository<Uom, Guid>>();
        Dictionary<string, bool> wholeNumberUoms = new(StringComparer.OrdinalIgnoreCase);
        if (uomRepo != null)
        {
            var uomNames = so.Items.Select(i => i.Uom).Distinct().ToList();
            var uomQuery = await uomRepo.GetQueryableAsync();
            wholeNumberUoms = uomQuery
                .Where(u => uomNames.Contains(u.Name))
                .Select(u => new { u.Name, u.MustBeWholeNumber })
                .ToDictionary(u => u.Name, u => u.MustBeWholeNumber, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var dnItem in dn.Items)
        {
            var soItem = dnItem.SalesOrderItemId.HasValue
                ? so.Items.FirstOrDefault(i => i.Id == dnItem.SalesOrderItemId.Value)
                : so.Items.FirstOrDefault(i => i.ItemId == dnItem.ItemId);
            if (soItem == null) continue;

            if (soItem.IsClosed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Item {dnItem.Description} is closed in Sales Order {so.OrderNumber} and cannot be processed further.");
            }

            var isWhole = wholeNumberUoms.GetValueOrDefault(soItem.Uom, false);
            var maxAllowedTotal = soItem.GetMaxDeliverableQty(allowancePct, isWhole);
            var remainingAllowed = maxAllowedTotal - soItem.DeliveredQty;

            if (dnItem.Quantity > remainingAllowed)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverDelivery)
                    .WithData("itemName", dnItem.Description)
                    .WithData("orderedQty", soItem.Quantity)
                    .WithData("deliveredQty", soItem.DeliveredQty)
                    .WithData("attemptedQty", dnItem.Quantity);
            }
        }
    }

    /// <summary>
    /// Validates return DN (goods return from customer) business rules.
    /// Per ERPNext PR #59820 (commit 13ccd6e49f), PR #59816 (commit dc898340dc), and PR #59823 (commit 1623fcde72):
    /// 1. All return rows must reference items delivered in the original DN.
    /// 2. Return quantities across all rows (with or without detail links) must not exceed total delivered qty net of prior returns.
    /// 3. Returns of batch items cannot exceed the quantity delivered from each batch net of prior returns.
    /// 4. Return rates cannot exceed original sale rates (weighted-average valuation exempt).
    /// </summary>
    public async Task ValidateReturnAsync(DeliveryNote returnDN)
    {
        if (!returnDN.IsReturn) return;

        if (!returnDN.ReturnAgainstId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnMustReferenceOriginal)
                .WithData("documentType", "Delivery Note");
        }

        // Returns must have negative quantities and at least one item with negative quantity
        if (returnDN.Items.Any(i => i.Quantity > 0) || !returnDN.Items.Any(i => i.Quantity < 0))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyMustBeNegative)
                .WithData("documentType", "Delivery Note");
        }

        var original = await _dnRepository.GetAsync(returnDN.ReturnAgainstId.Value);

        // Validate customer and company match original document (ERPNext PR #48588 / commit e073075834)
        if (original.CustomerId != returnDN.CustomerId || original.CompanyId != returnDN.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnPartyMismatch)
                .WithData("documentType", "Delivery Note")
                .WithData("returnCustomer", returnDN.CustomerId)
                .WithData("originalCustomer", original.CustomerId);
        }

        // Validate that all return items exist in the original Delivery Note (ERPNext PR #59820)
        var validItemIds = original.Items.Select(i => i.ItemId).ToHashSet();
        foreach (var returnItem in returnDN.Items)
        {
            if (!validItemIds.Contains(returnItem.ItemId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnItemNotFoundInOriginal)
                    .WithData("item", returnItem.Description)
                    .WithData("referenceDoc", original.DeliveryNumber);
            }
        }

        // Query prior submitted/posted returns against this same original delivery note
        var dnQuery = await _dnRepository.GetQueryableAsync();
        var priorReturnDocs = dnQuery
            .Where(dn => dn.ReturnAgainstId == original.Id
                && dn.Id != returnDN.Id
                && (dn.Status == Core.DocumentStatus.Submitted || dn.Status == Core.DocumentStatus.Posted))
            .ToList();

        var priorReturns = priorReturnDocs
            .SelectMany(dn => dn.Items)
            .ToList();

        // Seed accumulated return trackers with quantities from prior submitted returns
        var accumulatedReturnedByItem = priorReturns
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m))));

        // Seed batch-level returns: direct BatchId on DeliveryNoteItem
        var accumulatedReturnedBatchStock = priorReturns
            .Where(i => i.BatchId.HasValue)
            .GroupBy(i => (i.ItemId, i.BatchId!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m))));

        // Also check if SerialAndBatchBundle exists in LazyServiceProvider
        var bundleRepo = LazyServiceProvider?.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>();
        var originalBundleBatches = new Dictionary<(Guid ItemId, Guid BatchId), decimal>();
        var priorBundleReturnedBatches = new Dictionary<(Guid ItemId, Guid BatchId), decimal>();

        if (bundleRepo != null)
        {
            var priorReturnIds = priorReturnDocs.Select(d => d.Id).ToHashSet();
            var bundleQuery = await bundleRepo.GetQueryableAsync();
            var relevantBundles = bundleQuery
                .Where(b => (b.VoucherType == "DeliveryNote" || b.VoucherType == "Delivery Note")
                    && (b.VoucherId == original.Id || priorReturnIds.Contains(b.VoucherId))
                    && !b.IsCancelled)
                .ToList();

            foreach (var bundle in relevantBundles)
            {
                var isOrig = bundle.VoucherId == original.Id;
                foreach (var entry in bundle.Entries.Where(e => e.BatchId.HasValue))
                {
                    var key = (bundle.ItemId, entry.BatchId!.Value);
                    var entryQty = Math.Abs(entry.Qty);
                    if (isOrig)
                    {
                        originalBundleBatches[key] = originalBundleBatches.GetValueOrDefault(key, 0m) + entryQty;
                    }
                    else
                    {
                        priorBundleReturnedBatches[key] = priorBundleReturnedBatches.GetValueOrDefault(key, 0m) + entryQty;
                    }
                }
            }

            foreach (var kvp in priorBundleReturnedBatches)
            {
                accumulatedReturnedBatchStock[kvp.Key] = accumulatedReturnedBatchStock.GetValueOrDefault(kvp.Key, 0m) + kvp.Value;
            }
        }

        foreach (var returnItem in returnDN.Items)
        {
            var returnFactor = returnItem.ConversionFactor > 0 ? returnItem.ConversionFactor : 1m;
            // PR #59823: calculate current return stock qty directly from current qty & factor
            var returnStockQty = Math.Abs(returnItem.Quantity) * returnFactor;

            // Total delivered stock qty for this item across all original rows (PR #59820)
            var originalItemStockQty = original.Items
                .Where(i => i.ItemId == returnItem.ItemId)
                .Sum(i => i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m));

            var alreadyReturnedStock = accumulatedReturnedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
            var maxReturnableStock = originalItemStockQty - alreadyReturnedStock;

            if (returnStockQty > maxReturnableStock + 0.0000001m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItemStockQty / returnFactor)
                    .WithData("alreadyReturned", alreadyReturnedStock / returnFactor)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
            }

            // Accumulate returned qty for this item so multiple rows in this same return document are bounded (PR #59820)
            accumulatedReturnedByItem[returnItem.ItemId] = alreadyReturnedStock + returnStockQty;

            // Batch validation: limit sales returns to qty delivered from each batch (PR #59816)
            if (returnItem.BatchId.HasValue)
            {
                var batchKey = (returnItem.ItemId, returnItem.BatchId.Value);

                // Delivered batch qty from original DN items + bundles
                var originalDeliveredBatchStock = original.Items
                    .Where(i => i.ItemId == returnItem.ItemId && i.BatchId == returnItem.BatchId.Value)
                    .Sum(i => i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m))
                    + originalBundleBatches.GetValueOrDefault(batchKey, 0m);

                var alreadyReturnedBatchStock = accumulatedReturnedBatchStock.GetValueOrDefault(batchKey, 0m);
                var remainingBatchStock = originalDeliveredBatchStock - alreadyReturnedBatchStock;

                if (returnStockQty > remainingBatchStock + 0.0000001m)
                {
                    var batchRepo = LazyServiceProvider?.LazyGetService<IRepository<Batch, Guid>>();
                    var batch = batchRepo != null ? await batchRepo.FindAsync(returnItem.BatchId.Value) : null;
                    var batchNo = batch?.BatchNo ?? returnItem.BatchId.Value.ToString();

                    throw new BusinessException(MyERPDomainErrorCodes.ReturnBatchQtyExceedsDelivered)
                        .WithData("item", returnItem.Description)
                        .WithData("batch", batchNo)
                        .WithData("returnQty", Math.Abs(returnItem.Quantity))
                        .WithData("deliveredQty", Math.Max(0m, remainingBatchStock / returnFactor));
                }

                accumulatedReturnedBatchStock[batchKey] = alreadyReturnedBatchStock + returnStockQty;
            }

            // Return rate cannot exceed original sale rate — Moving Average items are exempt
            // (their rate legitimately fluctuates). Per returns-inter-company skill.
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem != null && returnItem.UnitPrice > originalItem.UnitPrice)
            {
                var item = await _itemRepository.FindAsync(returnItem.ItemId);
                if (item?.ValuationMethod != ValuationMethod.WeightedAverage)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ReturnRateExceedsOriginal)
                        .WithData("item", returnItem.Description)
                        .WithData("returnRate", returnItem.UnitPrice)
                        .WithData("originalRate", originalItem.UnitPrice);
                }
            }
        }
    }

    /// <summary>
    /// Prevents delivering items from sample retention warehouse per ERPNext PR #55613.
    /// </summary>
    public async Task ValidateSampleRetentionWarehouseAsync(DeliveryNote dn)
    {
        if (dn.IsReturn) return;

        var company = await _companyRepository.FindAsync(dn.CompanyId);
        if (company?.SampleRetentionWarehouseId == null) return;

        var sampleWarehouseId = company.SampleRetentionWarehouseId.Value;
        foreach (var item in dn.Items)
        {
            if (item.WarehouseId == sampleWarehouseId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CannotSellFromSampleRetentionWarehouse)
                    .WithData("itemName", item.Description)
                    .WithData("warehouseId", sampleWarehouseId);
            }
        }
    }
}
