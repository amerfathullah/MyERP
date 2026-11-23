using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Domain service for Purchase Receipt business rules.
/// Validates receipt against linked PO, return documents, and dependent cancellation guards.
/// </summary>
public class PurchaseReceiptManager : DomainService
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;
    private readonly IRepository<PurchaseReceipt, Guid> _prRepository;
    private readonly IRepository<Tax.Entities.TransactionTaxRow, Guid>? _taxRowRepository;

    public PurchaseReceiptManager(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> prRepository)
        : this(poRepository, prRepository, null)
    {
    }

    public PurchaseReceiptManager(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> prRepository,
        IRepository<Tax.Entities.TransactionTaxRow, Guid>? taxRowRepository)
    {
        _poRepository = poRepository;
        _prRepository = prRepository;
        _taxRowRepository = taxRowRepository;
    }

    /// <summary>
    /// Mandatory PO linkage: every PR item must reference a Purchase Order.
    /// Per ERPNext stock/doctype/purchase_receipt/purchase_receipt.py → po_required().
    /// Skipped for return receipts.
    /// </summary>
    public static void ValidatePoRequired(PurchaseReceipt receipt, bool poRequired)
    {
        if (!poRequired || receipt.IsReturn) return;

        var unlinkedItem = receipt.Items.FirstOrDefault(i => !i.PurchaseOrderItemId.HasValue);
        if (unlinkedItem != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PurchaseOrderLinkRequired)
                .WithData("itemDescription", unlinkedItem.Description);
        }
    }

    /// <summary>
    /// Validates receipt quantities and posting date against the linked Purchase Order.
    /// Prevents over-receipt and temporal ordering violations.
    /// <summary>
    /// Validates linked PO: active status, temporal ordering, and pending receipt limits.
    /// Per ERPNext PR #58126: purchase returns against a Closed PO are allowed (cancelled POs are always blocked).
    /// </summary>
    public async Task ValidateAgainstPurchaseOrderAsync(PurchaseReceipt receipt)
    {
        var poIds = new HashSet<Guid>();
        if (receipt.PurchaseOrderId.HasValue)
        {
            poIds.Add(receipt.PurchaseOrderId.Value);
        }

        var linkedPoItemIds = receipt.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (linkedPoItemIds.Count > 0)
        {
            var poQuery = await _poRepository.GetQueryableAsync();
            var itemPoIds = poQuery
                .Where(p => p.Items.Any(pi => linkedPoItemIds.Contains(pi.Id)))
                .Select(p => p.Id)
                .ToList();
            foreach (var pid in itemPoIds) poIds.Add(pid);
        }

        if (poIds.Count == 0) return;

        var poList = new List<PurchaseOrder>();
        foreach (var pid in poIds)
        {
            poList.Add(await _poRepository.GetAsync(pid));
        }

        // Validate combinability across multiple Purchase Orders (PR #60510 / commit 77e0c43997)
        if (poList.Count > 1)
        {
            await ValidateCombinablePurchaseOrdersAsync(receipt, poList);
        }

        foreach (var po in poList)
        {
            // PO must be in an active fulfillment state (returns allowed against Closed PO, but blocked against Cancelled)
            if (po.Status == Core.DocumentStatus.Cancelled || (!receipt.IsReturn && po.Status == Core.DocumentStatus.Closed))
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("documentType", "Purchase Order")
                    .WithData("status", po.Status.ToString());
            }

            // Return PRs bypass temporal ordering and pending quantity checks against PO
            if (receipt.IsReturn) continue;

            // Temporal ordering: cannot receive before ordering
            if (receipt.PostingDate.Date < po.OrderDate.Date)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PostingDateBeforePODate)
                    .WithData("postingDate", receipt.PostingDate)
                    .WithData("poDate", po.OrderDate)
                    .WithData("poNumber", po.OrderNumber);
            }

            foreach (var prItem in receipt.Items)
            {
                var poItem = prItem.PurchaseOrderItemId.HasValue
                    ? po.Items.FirstOrDefault(i => i.Id == prItem.PurchaseOrderItemId.Value)
                    : (po.Id == receipt.PurchaseOrderId ? po.Items.FirstOrDefault(i => i.ItemId == prItem.ItemId) : null);

                if (poItem != null)
                {
                    if (poItem.IsClosed)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"Item {prItem.Description} is closed in Purchase Order {po.OrderNumber} and cannot be processed further.");
                    }
                    var prFactor = prItem.ConversionFactor > 0 ? prItem.ConversionFactor : 1m;
                    var poFactor = poItem.ConversionFactor > 0 ? poItem.ConversionFactor : 1m;
                    var attemptedInPoUom = (prItem.Quantity * prFactor) / poFactor;

                    if (attemptedInPoUom > poItem.PendingReceiptQty)
                    {
                        throw new BusinessException("MyERP:08006")
                            .WithData("itemName", prItem.Description)
                            .WithData("orderedQty", poItem.Quantity)
                            .WithData("receivedQty", poItem.ReceivedQty)
                            .WithData("attemptedQty", attemptedInPoUom);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates asset returns per ERPNext PR #63606 / commit bb1ef484dc:
    /// Allows partial returns of asset purchases as long as the quantity of active (draft or submitted)
    /// assets linked to each purchase row/item does not exceed the remaining quantity kept after this return.
    /// Blocks returns if unlinked submitted assets exist on the original document, or if active asset count
    /// exceeds the kept quantity.
    /// </summary>
    public async Task ValidateAssetReturnAsync(
        PurchaseReceipt returnReceipt,
        IRepository<Asset, Guid> assetRepository)
    {
        if (!returnReceipt.IsReturn || !returnReceipt.ReturnAgainstId.HasValue) return;

        var originalId = returnReceipt.ReturnAgainstId.Value;
        var assetQuery = await assetRepository.GetQueryableAsync();
        var linkedAssets = assetQuery
            .Where(a => a.PurchaseReceiptId == originalId && a.Status != AssetStatus.Cancelled)
            .ToList();

        if (linkedAssets.Count == 0) return;

        var original = await _prRepository.FindAsync(originalId);
        if (original == null) return;

        // If any submitted asset is not linked to an ItemId that exists on original PR,
        // it cannot be attributed to a specific retained quantity (ERPNext PR #63606).
        var unlinkedSubmittedAssets = linkedAssets
            .Where(a => a.Status != AssetStatus.Draft
                && (!a.ItemId.HasValue || !original.Items.Any(oi => oi.ItemId == a.ItemId.Value)))
            .ToList();

        if (unlinkedSubmittedAssets.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                .WithData("documentType", "Purchase Receipt")
                .WithData("returnAgainst", returnReceipt.ReturnAgainstId.Value);
        }

        // Query prior submitted/posted returns against this same original purchase receipt
        var prQuery = await _prRepository.GetQueryableAsync();
        var priorReturns = prQuery
            .Where(pr => pr.ReturnAgainstId == originalId
                && pr.Id != returnReceipt.Id
                && (pr.Status == Core.DocumentStatus.Submitted || pr.Status == Core.DocumentStatus.Posted))
            .SelectMany(pr => pr.Items)
            .ToList();

        var priorReturnedByItem = priorReturns
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity)));

        var currentReturnedByItem = returnReceipt.Items
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity)));

        foreach (var (itemId, returnQty) in currentReturnedByItem)
        {
            var originalQty = original.Items.Where(i => i.ItemId == itemId).Sum(i => i.Quantity);
            var priorReturned = priorReturnedByItem.GetValueOrDefault(itemId, 0m);
            var keptQty = originalQty - priorReturned - returnQty;

            var activeAssetsForItem = linkedAssets
                .Where(a => a.ItemId == itemId)
                .Sum(a => a.AssetQuantity > 0 ? a.AssetQuantity : 1m);

            if (activeAssetsForItem > keptQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                    .WithData("documentType", "Purchase Receipt")
                    .WithData("returnAgainst", returnReceipt.ReturnAgainstId.Value)
                    .WithData("keptQty", keptQty)
                    .WithData("activeAssetCount", activeAssetsForItem);
            }
        }
    }

    /// <summary>
    /// Validates from_warehouse rules on purchase document items.
    /// (1) from_warehouse cannot equal target warehouse (no-op transfer blocked)
    /// (2) from_warehouse cannot be set on subcontracted documents
    /// </summary>
    public void ValidateFromWarehouse(PurchaseReceipt receipt)
    {
        foreach (var item in receipt.Items)
        {
            if (item.FromWarehouseId.HasValue && item.FromWarehouseId == item.WarehouseId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseEqualsTargetWarehouse)
                    .WithData("row", item.Description);
            }

            if (item.FromWarehouseId.HasValue && receipt.IsSubcontracted)
            {
                throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseOnSubcontractedDocument)
                    .WithData("row", item.Description);
            }
        }
    }

    /// <summary>
    /// Validates return receipt (goods return to supplier) business rules.
    /// Return qty per item cannot exceed original receipt qty.
    /// </summary>
    public async Task ValidateReturnAsync(PurchaseReceipt returnReceipt)
    {
        if (!returnReceipt.IsReturn || !returnReceipt.ReturnAgainstId.HasValue) return;

        // Must have negative quantities and at least one item with negative quantity or negative rejected quantity (PR #57645 / PR #59280)
        if (returnReceipt.Items.Any(i => i.Quantity > 0 || i.RejectedQty > 0) ||
            !returnReceipt.Items.Any(i => i.Quantity < 0 || i.RejectedQty < 0))
        {
            throw new BusinessException("MyERP:08001")
                .WithData("documentType", "Purchase Receipt");
        }

        var original = await _prRepository.GetAsync(returnReceipt.ReturnAgainstId.Value);

        // Validate supplier and company match original document (ERPNext PR #48588 / commit e073075834)
        if (original.SupplierId != returnReceipt.SupplierId || original.CompanyId != returnReceipt.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnPartyMismatch)
                .WithData("documentType", "Purchase Receipt")
                .WithData("returnSupplier", returnReceipt.SupplierId)
                .WithData("originalSupplier", original.SupplierId);
        }

        // Query prior submitted/posted returns against this same original purchase receipt
        var prQuery = await _prRepository.GetQueryableAsync();
        var priorReturns = prQuery
            .Where(pr => pr.ReturnAgainstId == original.Id
                && pr.Id != returnReceipt.Id
                && (pr.Status == Core.DocumentStatus.Submitted || pr.Status == Core.DocumentStatus.Posted))
            .SelectMany(pr => pr.Items)
            .ToList();

        var priorReturnedByItem = priorReturns
            .Where(i => !i.ReturnQtyFromRejectedWarehouse)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m))));

        var priorReturnedRejectedByItem = priorReturns
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (Math.Abs(i.RejectedQty) + (i.ReturnQtyFromRejectedWarehouse ? Math.Abs(i.Quantity) : 0m)) * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m)));

        // Validate that all return items exist in the original Purchase Receipt (ERPNext PR #59820)
        var validItemIds = original.Items.Select(i => i.ItemId).ToHashSet();
        foreach (var returnItem in returnReceipt.Items)
        {
            if (!validItemIds.Contains(returnItem.ItemId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnItemNotFoundInOriginal)
                    .WithData("item", returnItem.Description)
                    .WithData("referenceDoc", original.ReceiptNumber);
            }
        }

        // Return qty per item cannot exceed (original qty - already_returned)
        // Uses stock qty comparison to support different UOM returns (ERPNext commit abf94bc72d, PR #59280).
        var accumulatedReturnedByItem = priorReturnedByItem.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var accumulatedReturnedRejectedByItem = priorReturnedRejectedByItem.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var returnItem in returnReceipt.Items)
        {
            var returnFactor = returnItem.ConversionFactor > 0 ? returnItem.ConversionFactor : 1m;

            // Validate accepted or rejected-warehouse return (PR #60672 / commit 4cc2ffb2a8)
            if (returnItem.Quantity < 0)
            {
                var returnStockQty = Math.Abs(returnItem.Quantity) * returnFactor;

                if (returnItem.ReturnQtyFromRejectedWarehouse)
                {
                    var originalRejectedStock = original.Items
                        .Where(i => i.ItemId == returnItem.ItemId)
                        .Sum(i => i.RejectedQty * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m));

                    var alreadyReturnedRejected = accumulatedReturnedRejectedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
                    var maxReturnableRejected = originalRejectedStock - alreadyReturnedRejected;

                    if (returnStockQty > maxReturnableRejected + 0.0000001m)
                    {
                        throw new BusinessException("MyERP:08004")
                            .WithData("itemName", returnItem.Description)
                            .WithData("originalQty", originalRejectedStock / returnFactor)
                            .WithData("alreadyReturned", alreadyReturnedRejected / returnFactor)
                            .WithData("returnQty", Math.Abs(returnItem.Quantity));
                    }

                    accumulatedReturnedRejectedByItem[returnItem.ItemId] = alreadyReturnedRejected + returnStockQty;
                }
                else
                {
                    var originalStockQty = original.Items
                        .Where(i => i.ItemId == returnItem.ItemId)
                        .Sum(i => i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m));

                    var alreadyReturnedStock = accumulatedReturnedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
                    var maxReturnableStock = originalStockQty - alreadyReturnedStock;

                    if (returnStockQty > maxReturnableStock + 0.0000001m)
                    {
                        throw new BusinessException("MyERP:08004")
                            .WithData("itemName", returnItem.Description)
                            .WithData("originalQty", originalStockQty / returnFactor)
                            .WithData("alreadyReturned", alreadyReturnedStock / returnFactor)
                            .WithData("returnQty", Math.Abs(returnItem.Quantity));
                    }

                    accumulatedReturnedByItem[returnItem.ItemId] = alreadyReturnedStock + returnStockQty;
                }
            }

            // Validate rejected qty return (PR #59280 / commit b3d55db893)
            if (returnItem.RejectedQty < 0)
            {
                var returnRejectedStock = Math.Abs(returnItem.RejectedQty) * returnFactor;
                var originalRejectedStock = original.Items
                    .Where(i => i.ItemId == returnItem.ItemId)
                    .Sum(i => i.RejectedQty * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m));

                var alreadyReturnedRejected = accumulatedReturnedRejectedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
                var maxReturnableRejected = originalRejectedStock - alreadyReturnedRejected;

                if (returnRejectedStock > maxReturnableRejected + 0.0000001m)
                {
                    throw new BusinessException("MyERP:08004")
                        .WithData("itemName", returnItem.Description)
                        .WithData("originalQty", originalRejectedStock / returnFactor)
                        .WithData("alreadyReturned", alreadyReturnedRejected / returnFactor)
                        .WithData("returnQty", Math.Abs(returnItem.RejectedQty));
                }

                accumulatedReturnedRejectedByItem[returnItem.ItemId] = alreadyReturnedRejected + returnRejectedStock;
            }
        }
    }

    /// <summary>
    /// Validates a PR can be cancelled — blocks if submitted Purchase Invoices reference this receipt.
    /// Per DO-NOT: must cancel children first.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        PurchaseReceipt receipt,
        IRepository<PurchaseInvoice, Guid> piRepository)
    {
        var piQuery = await piRepository.GetQueryableAsync();
        var prItemIds = receipt.Items.Select(i => i.Id).ToList();
        var hasSubmittedPI = piQuery.Any(pi =>
            pi.Items.Any(i => i.PurchaseReceiptItemId.HasValue && prItemIds.Contains(i.PurchaseReceiptItemId.Value))
            && pi.Status != Core.DocumentStatus.Draft
            && pi.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedPI)
        {
            throw new BusinessException("MyERP:01010")
                .WithData("documentType", "Purchase Receipt")
                .WithData("dependent", "Purchase Invoice");
        }
    }

    /// <summary>
    /// Validates that items are not duplicated when AllowMultipleItems is disabled in Buying Settings.
    /// Per ERPNext: rows from different purchase orders do not count as duplicates.
    /// </summary>
    public void ValidateDuplicateItems(PurchaseReceipt receipt, bool allowMultipleItems)
    {
        if (allowMultipleItems)
            return;

        var duplicates = receipt.Items
            .GroupBy(i => (i.ItemId, i.PurchaseOrderItemId))
            .Any(g => g.Count() > 1);

        if (duplicates)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Same item cannot be entered multiple times.");
        }
    }

    /// <summary>
    /// Validates whether multiple Purchase Orders can be combined into a single Purchase Receipt.
    /// Per ERPNext PR #60510 (commit 77e0c43997 / 7fda3d0b4f):
    /// "One Purchase Receipt carries one currency, one set of rate-based taxes and one header discount."
    /// A percentage discount can be shared by orders; an amount discount belongs to its order alone.
    /// Rate-based taxes (plus actual charges if referenced by previous rows) must match across orders.
    /// </summary>
    public async Task ValidateCombinablePurchaseOrdersAsync(PurchaseReceipt receipt, IReadOnlyList<PurchaseOrder> orders)
    {
        if (orders.Count < 2) return;

        // 1. Currency validation: orders must all share the receipt's currency
        var currencies = orders.Select(o => o.CurrencyCode).Distinct().ToList();
        if (currencies.Count > 1 || (receipt != null && orders.Any(o => !string.Equals(o.CurrencyCode, receipt.CurrencyCode, StringComparison.OrdinalIgnoreCase))))
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCombinePurchaseOrders)
                .WithData("orders", string.Join(", ", orders.Select(o => o.OrderNumber)))
                .WithData("reason", "Currencies do not match.");
        }

        // 2. Discount terms validation
        // Per ERPNext: an amount discount belongs to its order alone; a percentage discount can be shared.
        var hasAmountDiscount = orders.Any(o => o.DiscountAmount > 0);
        if (hasAmountDiscount)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCombinePurchaseOrders)
                .WithData("orders", string.Join(", ", orders.Select(o => o.OrderNumber)))
                .WithData("reason", "Orders with header discount amounts cannot be combined.");
        }

        var discountTerms = orders.Select(o => o.AdditionalDiscountPercentage > 0
            ? (o.ApplyDiscountOn ?? "Net Total", o.AdditionalDiscountPercentage)
            : ("None", 0m)).Distinct().ToList();

        if (discountTerms.Count > 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCombinePurchaseOrders)
                .WithData("orders", string.Join(", ", orders.Select(o => o.OrderNumber)))
                .WithData("reason", "Orders have different discount percentages or discount basis.");
        }

        // 3. Tax terms validation
        if (_taxRowRepository != null)
        {
            var taxQueryable = await _taxRowRepository.GetQueryableAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var allTaxes = taxQueryable
                .Where(t => t.ParentType == "PurchaseOrder" && orderIds.Contains(t.ParentId))
                .ToList();

            var taxesByOrder = allTaxes.GroupBy(t => t.ParentId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.RowIndex).ToList());

            var orderTaxSignatures = new List<string>();
            foreach (var order in orders)
            {
                var taxes = taxesByOrder.GetValueOrDefault(order.Id, new List<Tax.Entities.TransactionTaxRow>());
                var hasRowBasedTax = taxes.Any(t => t.ChargeType is "On Previous Row Amount" or "On Previous Row Total");
                var rateBasedTaxes = taxes
                    .Where(t => hasRowBasedTax || t.ChargeType != "Actual")
                    .OrderBy(t => t.RowIndex)
                    .Select(t => $"{t.ChargeType}|{t.Rate}|{t.AccountId}|{t.CostCenterId}|{t.IncludedInPrintRate}|{t.AddDeductTax}|{t.TaxCategory}")
                    .ToList();

                orderTaxSignatures.Add(string.Join(";", rateBasedTaxes));
            }

            if (orderTaxSignatures.Distinct().Count() > 1)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CannotCombinePurchaseOrders)
                    .WithData("orders", string.Join(", ", orders.Select(o => o.OrderNumber)))
                    .WithData("reason", "Orders have different rate-based taxes or tax charge terms.");
            }
        }
    }

    /// <summary>
    /// Computes the currency exchange rate difference for a Purchase Receipt item billed across different exchange rates
    /// per ERPNext commit ce1484591d (add_lcv_gl_entries):
    /// Rounds qty * netRate * (purchaseInvoiceExchangeRate - purchaseReceiptExchangeRate) to baseNetAmountPrecision
    /// before checking non-zero or posting to Exchange Gain/Loss, preventing sub-cent residuals from outliving
    /// GL Debit/Credit rounding.
    /// </summary>
    public static decimal CalculateExchangeRateDifference(
        decimal qty,
        decimal netRate,
        decimal purchaseInvoiceExchangeRate,
        decimal purchaseReceiptExchangeRate,
        int baseNetAmountPrecision = 2)
    {
        var rawDiff = qty * netRate * (purchaseInvoiceExchangeRate - purchaseReceiptExchangeRate);
        return Math.Round(rawDiff, baseNetAmountPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Builds the Debit/Credit and Account-Currency Debit/Credit amounts for a Purchase Receipt
    /// exchange rate difference GL row per ERPNext commit 7eab0353ba (gl_composer.py):
    /// Ensures Credit rows populate <c>CreditInAccountCurrency</c> (not negative <c>DebitInAccountCurrency</c>)
    /// and Debit rows populate positive <c>DebitInAccountCurrency</c>.
    /// </summary>
    public static (decimal Debit, decimal Credit, decimal DebitInAccountCurrency, decimal CreditInAccountCurrency)
        BuildExchangeRateDifferenceGlSides(decimal exchangeRateDifference, int precision = 2)
    {
        var rounded = Math.Round(exchangeRateDifference, precision, MidpointRounding.AwayFromZero);
        if (rounded > 0m)
        {
            return (0m, rounded, 0m, rounded);
        }

        if (rounded < 0m)
        {
            var abs = Math.Abs(rounded);
            return (abs, 0m, abs, 0m);
        }

        return (0m, 0m, 0m, 0m);
    }

    /// <summary>
    /// Resolves the stock quantity that an internal Purchase Receipt row takes out of the in-transit
    /// source warehouse per ERPNext commits 99e9697f80, 5b81b52467, 82aa27155f, and d510a998d1:
    /// - Non-internal receipts, return receipts, rows without a linked Delivery Note item, or rows with
    ///   an accepted or rejected Serial and Batch Bundle take their full <paramref name="receivedStockQty"/>.
    /// - When the cumulative unrounded stock qty received across all receipts (and their returns) for the
    ///   same Delivery Note item exceeds <paramref name="deliveredStockQtyInTransit"/> (an over-receipt),
    ///   no transit cap is applied (only UOM rounding is capped).
    /// - Otherwise caps the row's source warehouse qty to <c>Min(receivedStockQty, deliveredStockQtyInTransit + otherReceiptsNetTakenFromTransit)</c>.
    /// </summary>
    public static decimal ResolveInternalReceiptSourceWarehouseStockQty(
        decimal receivedStockQty,
        bool isInternalReceipt,
        bool isReturn,
        Guid? deliveryNoteItemId,
        bool hasSerialOrBatchPackage,
        decimal deliveredStockQtyInTransit,
        decimal currentRowUnroundedReceivedStockQty,
        decimal otherReceiptsAndReturnsUnroundedReceivedStockQty,
        decimal otherReceiptsAndReturnsSleActualQtyInTransit,
        int stockQtyPrecision = 4)
    {
        if (!isInternalReceipt ||
            isReturn ||
            !deliveryNoteItemId.HasValue ||
            deliveryNoteItemId.Value == Guid.Empty ||
            hasSerialOrBatchPackage ||
            deliveredStockQtyInTransit <= 0m)
        {
            return receivedStockQty;
        }

        var cumulativeReceived = Math.Round(
            otherReceiptsAndReturnsUnroundedReceivedStockQty + currentRowUnroundedReceivedStockQty,
            stockQtyPrecision,
            MidpointRounding.AwayFromZero);

        if (cumulativeReceived > deliveredStockQtyInTransit)
        {
            return receivedStockQty;
        }

        // otherReceiptsAndReturnsSleActualQtyInTransit is negative when prior receipts took stock out of transit
        // and positive for what receipt returns put back into transit.
        var qtyLeftInTransit = Math.Round(
            deliveredStockQtyInTransit + otherReceiptsAndReturnsSleActualQtyInTransit,
            stockQtyPrecision,
            MidpointRounding.AwayFromZero);

        return Math.Min(receivedStockQty, qtyLeftInTransit);
    }

    /// <summary>
    /// Resolves the stock quantity that an internal Purchase Receipt Return row puts back into the
    /// in-transit source warehouse per ERPNext commits bebce66db8, 42c7a9d78b, and efa1c14797:
    /// - Only counts what the original receipt row actually took out of transit (<paramref name="originalReceiptOutwardSleActualQtyInTransit"/> &lt; 0,
    ///   ignoring rejected stock that stayed in transit) and what other submitted returns already put back into transit
    ///   (<paramref name="otherReturnsInwardSleActualQtyInTransit"/> &gt; 0).
    /// - Caps the return's negative stock quantity to <c>Max(returnStockQty, -qtyLeftToReturnToTransit)</c>
    ///   so returns never put more back into transit than the original receipt took out.
    /// </summary>
    public static decimal ResolveInternalReceiptReturnSourceWarehouseStockQty(
        decimal returnStockQty,
        bool isInternalReceipt,
        bool isReturn,
        Guid? originalPurchaseReceiptItemId,
        bool hasSerialOrBatchPackage,
        decimal originalReceiptOutwardSleActualQtyInTransit,
        decimal otherReturnsInwardSleActualQtyInTransit,
        int stockQtyPrecision = 4)
    {
        if (!isInternalReceipt ||
            !isReturn ||
            !originalPurchaseReceiptItemId.HasValue ||
            originalPurchaseReceiptItemId.Value == Guid.Empty ||
            hasSerialOrBatchPackage)
        {
            return returnStockQty;
        }

        var outwardOnly = originalReceiptOutwardSleActualQtyInTransit < 0m
            ? originalReceiptOutwardSleActualQtyInTransit
            : -Math.Abs(originalReceiptOutwardSleActualQtyInTransit);
        var inwardOnly = otherReturnsInwardSleActualQtyInTransit > 0m
            ? otherReturnsInwardSleActualQtyInTransit
            : Math.Abs(otherReturnsInwardSleActualQtyInTransit);

        var qtyLeftToReturn = Math.Round(-(outwardOnly + inwardOnly), stockQtyPrecision, MidpointRounding.AwayFromZero);
        if (qtyLeftToReturn <= 0m)
        {
            return returnStockQty;
        }

        var negativeReturnQty = returnStockQty <= 0m ? returnStockQty : -returnStockQty;
        return Math.Max(negativeReturnQty, -qtyLeftToReturn);
    }

    /// <summary>
    /// Looks up the stock quantity of a row's posted (or cancelled) Stock Ledger Entry in a transit warehouse
    /// per ERPNext PR #68435 (commit 2cb8e068a1) and PR #68373 (commit 95a51eabe3, <c>get_posted_qty</c>):
    /// Explicitly orders matching entries by <c>CreationTime ASC</c> so when <paramref name="cancelled"/> is true,
    /// the oldest (original) cancelled entry is taken rather than a newer reversal of a rejected-into-transit entry.
    /// </summary>
    public static decimal? GetPostedTransitSleQty(
        IEnumerable<Inventory.Entities.StockLedgerEntry>? entries,
        Guid voucherDetailNo,
        Guid warehouseId,
        bool outward,
        bool cancelled = false)
    {
        if (entries == null || voucherDetailNo == Guid.Empty || warehouseId == Guid.Empty)
        {
            return null;
        }

        var match = entries
            .Where(e =>
                e.VoucherDetailNo == voucherDetailNo &&
                e.WarehouseId == warehouseId &&
                e.IsCancelled == cancelled &&
                (outward ? e.QuantityChange < 0m : e.QuantityChange > 0m))
            .OrderBy(e => e.CreationTime)
            .FirstOrDefault();

        return match?.QuantityChange;
    }

    /// <summary>
    /// Resolves the in-transit source warehouse quantity for an internal Purchase Receipt (or Return) row
    /// when reposted (e.g. by a Landed Cost Voucher or SLE repost) or cancelled, per ERPNext PR #68435
    /// (commit 2cb8e068a1) and PR #68373 (commit 95a51eabe3, <c>get_qty_for_transit</c>):
    /// - On cancel (<paramref name="isCancelling"/>): reverses what the active SLE posted.
    /// - On repost: if an oldest cancelled SLE exists for the row in <paramref name="fromWarehouseId"/>,
    ///   reuses <c>-repostedQty</c> so returns posted after the receipt do not alter the original transit quantity.
    /// - Otherwise falls back to <paramref name="computedTransitQty"/>.
    /// </summary>
    public static decimal ResolveRepostedInternalReceiptTransitQty(
        decimal computedTransitQty,
        bool isCancelling,
        bool isReturn,
        Guid voucherDetailNo,
        Guid fromWarehouseId,
        IEnumerable<Inventory.Entities.StockLedgerEntry>? existingSles)
    {
        if (isCancelling)
        {
            var activeQty = GetPostedTransitSleQty(
                existingSles, voucherDetailNo, fromWarehouseId, outward: !isReturn, cancelled: false);
            return activeQty.HasValue && activeQty.Value != 0m
                ? -activeQty.Value
                : computedTransitQty;
        }

        var repostedQty = GetPostedTransitSleQty(
            existingSles, voucherDetailNo, fromWarehouseId, outward: !isReturn, cancelled: true);
        if (repostedQty.HasValue && repostedQty.Value != 0m)
        {
            return -repostedQty.Value;
        }

        return computedTransitQty;
    }

    /// <summary>
    /// Determines whether an internal receipt or return row has a non-zero rejected stock quantity in stock UOM
    /// requiring a rejected Serial and Batch Bundle, and computes the rounded package stock quantity
    /// per ERPNext commits 33410f737d, ae011f50fc, and ca0c0ce86a.
    /// </summary>
    public static (bool ShouldCreatePackage, decimal RoundedStockQty) EvaluateInternalReceiptPackageStockQty(
        decimal qtyInTransactionUom,
        decimal conversionFactor,
        int stockQtyPrecision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var roundedStockQty = Math.Round(qtyInTransactionUom * factor, stockQtyPrecision, MidpointRounding.AwayFromZero);
        return (Math.Abs(roundedStockQty) > 0m, roundedStockQty);
    }

    /// <summary>
    /// Computes the transit valuation amount of an internal transfer receipt or return row at its incoming transit rate
    /// per ERPNext commits 86229973c0 and cceafa6af4 (get_transit_amount):
    /// Values the row at the rounded stock quantity that the Stock Ledger Entry moves (<c>Round(qty * conversionFactor, stockQtyPrecision) * salesIncomingRate / conversionFactor</c>).
    /// </summary>
    public static decimal CalculateInternalTransferTransitAmount(
        decimal qtyInTransactionUom,
        decimal conversionFactor,
        decimal salesIncomingRate,
        int stockQtyPrecision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var roundedStockQty = Math.Round(qtyInTransactionUom * factor, stockQtyPrecision, MidpointRounding.AwayFromZero);
        return (roundedStockQty * salesIncomingRate) / factor;
    }

    /// <summary>
    /// Resolves the transit stock value and warehouse debit amount for an internal transfer Purchase Invoice (or Return)
    /// per ERPNext commits 5c38112008, 1d5eb2a43d, 0c17bbd80c, 42fb302bca, e661476a74, and d0942983e5:
    /// - Returns <c>TransitStockValue = null</c> when not an internal transfer, no <paramref name="fromWarehouseId"/>,
    ///   when <paramref name="rejectedWarehouseId"/> equals <paramref name="fromWarehouseId"/> (rejected material stays in transit, e661476a74),
    ///   or when no SLE exists in the transit warehouse (<paramref name="transitWarehouseSleStockValue"/> is null).
    /// - Preserves an explicit <c>0m</c> transit SLE value (<c>42fb302bca</c>).
    /// - Computes <c>WarehouseDebitAmount = Round(Round(TransitStockValue, precision) - Round(RejectedSleStockValue, precision) + LandedCostVoucherAmount, precision)</c>.
    /// </summary>
    public static (decimal? TransitStockValue, decimal WarehouseDebitAmount) ResolveInternalTransferInvoiceTransitAndWarehouseGlAmounts(
        bool isInternalTransfer,
        Guid? fromWarehouseId,
        Guid? rejectedWarehouseId,
        decimal? transitWarehouseSleStockValue,
        decimal? rejectedWarehouseSleStockValue,
        decimal landedCostVoucherAmount,
        decimal fallbackWarehouseDebitAmount,
        int netAmountPrecision = 2)
    {
        if (!isInternalTransfer ||
            !fromWarehouseId.HasValue ||
            fromWarehouseId.Value == Guid.Empty ||
            (rejectedWarehouseId.HasValue && rejectedWarehouseId.Value == fromWarehouseId.Value) ||
            !transitWarehouseSleStockValue.HasValue)
        {
            return (null, Math.Round(fallbackWarehouseDebitAmount, netAmountPrecision, MidpointRounding.AwayFromZero));
        }

        var transitStockValue = -transitWarehouseSleStockValue.Value;
        var roundedTransit = Math.Round(transitStockValue, netAmountPrecision, MidpointRounding.AwayFromZero);
        var roundedRejected = Math.Round(rejectedWarehouseSleStockValue ?? 0m, netAmountPrecision, MidpointRounding.AwayFromZero);
        var warehouseDebit = Math.Round(
            roundedTransit - roundedRejected + landedCostVoucherAmount,
            netAmountPrecision,
            MidpointRounding.AwayFromZero);

        return (transitStockValue, warehouseDebit);
    }

    /// <summary>
    /// Computes the negative returned share of each Actual valuation charge on a Purchase Receipt Return
    /// or stock-updating Purchase Invoice Return (Debit Note) per ERPNext commits d8a07e8d66, ff4f660d34,
    /// 235499af25, ae1ba15068, 5fe1e4f6cc, 703f40f23b, and 28a9b6ff5b (ReturnChargesService):
    /// - If any return item row is not linked to an original row (<c>!OriginalRowId.HasValue</c>), leaves the return's charges unchanged.
    /// - Matches each return Actual valuation charge to the original document's Actual valuation charges by <c>(AccountId, Description)</c> in order.
    /// - Spreads the original charge's <c>BaseTaxAmount</c> (company currency) across original items, scales by the returned stock qty
    ///   over the original row's carried stock qty, and converts back to transaction currency via <paramref name="returnConversionRate"/>.
    /// </summary>
    public static List<(Guid ReturnChargeId, decimal TaxAmount)> CalculateReturnedActualValuationCharges(
        IReadOnlyList<(Guid ReturnChargeId, Guid AccountId, string? Description, decimal CurrentTaxAmount)> returnCharges,
        IReadOnlyList<(Guid AccountId, string? Description, decimal BaseTaxAmount, bool AllocateFullAmountToStockItems)> originalCharges,
        IReadOnlyList<(Guid OriginalRowId, bool IsStockOrAssetItem, decimal AcceptedStockQty, decimal RejectedStockQty, decimal ItemShareRatio)> originalItems,
        IReadOnlyList<(Guid? OriginalRowId, decimal AcceptedStockQty, decimal RejectedStockQty, bool ReturnQtyFromRejectedWarehouse)> returnItems,
        bool billsRejectedQuantity = false,
        decimal returnConversionRate = 1m,
        int taxAmountPrecision = 2)
    {
        var result = new List<(Guid ReturnChargeId, decimal TaxAmount)>();
        if (returnCharges == null || returnCharges.Count == 0)
        {
            return result;
        }

        if (returnItems == null ||
            returnItems.Count == 0 ||
            returnItems.Any(r => !r.OriginalRowId.HasValue || r.OriginalRowId.Value == Guid.Empty) ||
            originalCharges == null ||
            originalItems == null)
        {
            foreach (var rc in returnCharges)
            {
                result.Add((rc.ReturnChargeId, rc.CurrentTaxAmount));
            }
            return result;
        }

        var returnedByOrigRow = new Dictionary<Guid, (decimal Accepted, decimal Rejected)>();
        foreach (var row in returnItems)
        {
            var origId = row.OriginalRowId!.Value;
            var cur = returnedByOrigRow.GetValueOrDefault(origId, (Accepted: 0m, Rejected: 0m));
            decimal addAccepted, addRejected;
            if (row.ReturnQtyFromRejectedWarehouse)
            {
                addAccepted = 0m;
                addRejected = Math.Abs(row.AcceptedStockQty);
            }
            else
            {
                addAccepted = Math.Abs(row.AcceptedStockQty);
                addRejected = Math.Abs(row.RejectedStockQty);
            }
            returnedByOrigRow[origId] = (Accepted: cur.Accepted + addAccepted, Rejected: cur.Rejected + addRejected);
        }

        var origChargeQueues = new Dictionary<(Guid AccountId, string Description), Queue<(decimal BaseTaxAmount, bool AllocateFullAmountToStockItems)>>();
        foreach (var oc in originalCharges)
        {
            var key = (oc.AccountId, (oc.Description ?? string.Empty).Trim());
            if (!origChargeQueues.TryGetValue(key, out var q))
            {
                q = new Queue<(decimal, bool)>();
                origChargeQueues[key] = q;
            }
            q.Enqueue((oc.BaseTaxAmount, oc.AllocateFullAmountToStockItems));
        }

        var fx = returnConversionRate > 0m ? returnConversionRate : 1m;
        foreach (var rc in returnCharges)
        {
            var key = (rc.AccountId, (rc.Description ?? string.Empty).Trim());
            if (!origChargeQueues.TryGetValue(key, out var q) || q.Count == 0)
            {
                result.Add((rc.ReturnChargeId, rc.CurrentTaxAmount));
                continue;
            }

            var (baseTaxAmount, stockOnly) = q.Dequeue();
            var eligibleItems = stockOnly
                ? originalItems.Where(i => i.IsStockOrAssetItem).ToList()
                : originalItems.ToList();

            var totalRatio = eligibleItems.Sum(i => i.ItemShareRatio);
            decimal baseShare = 0m;

            foreach (var origRow in eligibleItems)
            {
                var rowCharge = totalRatio > 0m
                    ? baseTaxAmount * (origRow.ItemShareRatio / totalRatio)
                    : (eligibleItems.Count > 0 ? baseTaxAmount / eligibleItems.Count : 0m);

                var retQty = returnedByOrigRow.GetValueOrDefault(origRow.OriginalRowId, (Accepted: 0m, Rejected: 0m));
                decimal carriedQty;
                decimal returnedStock;

                if (origRow.AcceptedStockQty != 0m)
                {
                    carriedQty = origRow.AcceptedStockQty + (billsRejectedQuantity ? origRow.RejectedStockQty : 0m);
                    returnedStock = retQty.Accepted + (billsRejectedQuantity ? retQty.Rejected : 0m);
                }
                else
                {
                    carriedQty = origRow.RejectedStockQty;
                    returnedStock = retQty.Rejected;
                }

                if (carriedQty != 0m)
                {
                    baseShare += rowCharge * (returnedStock / carriedQty);
                }
            }

            var taxAmount = -Math.Round(baseShare / fx, taxAmountPrecision, MidpointRounding.AwayFromZero);
            result.Add((rc.ReturnChargeId, taxAmount));
        }

        return result;
    }

    /// <summary>
    /// Computes the remaining quantity to order on a Purchase Order in the source row's own UOM
    /// from the unrounded stock quantities of submitted Purchase Orders per ERPNext commits 393217f004 and c4c278ceb9.
    /// </summary>
    public static decimal CalculateRemainingQtyToOrderInRowUom(
        decimal sourceRowQty,
        decimal sourceRowConversionFactor,
        IEnumerable<(decimal OrderedQty, decimal OrderedConversionFactor)>? submittedPurchaseOrderRows,
        int qtyPrecision = 4)
    {
        var factor = sourceRowConversionFactor > 0m ? sourceRowConversionFactor : 1m;
        var orderedUnroundedStockQty = submittedPurchaseOrderRows != null
            ? submittedPurchaseOrderRows.Sum(r => r.OrderedQty * (r.OrderedConversionFactor > 0m ? r.OrderedConversionFactor : 1m))
            : 0m;
        var remaining = Math.Round(sourceRowQty - (orderedUnroundedStockQty / factor), qtyPrecision, MidpointRounding.AwayFromZero);
        return Math.Max(0m, remaining);
    }

    /// <summary>
    /// Aggregates ordered or received stock quantity across partial Purchase Order / Purchase Receipt / Purchase Invoice rows
    /// per ERPNext commits 0e77c8d15e, 21d02fa8bf, 127f4070e0, and 04b2ca0ed1:
    /// When <paramref name="sumUnroundedProduct"/> is true, sums <c>Qty * ConversionFactor</c> across rows instead of
    /// pre-rounded <c>RoundedStockQty</c>, and rounds the total once at the end only when <paramref name="roundTotalToTargetPrecision"/> is true.
    /// </summary>
    public static decimal AggregateSourceOrderedOrReceivedStockQty(
        IEnumerable<(decimal Qty, decimal ConversionFactor, decimal RoundedStockQty)>? rows,
        bool sumUnroundedProduct,
        bool roundTotalToTargetPrecision,
        int targetPrecision = 4)
    {
        if (rows == null)
        {
            return 0m;
        }

        if (!sumUnroundedProduct)
        {
            return rows.Sum(r => r.RoundedStockQty);
        }

        var unroundedSum = rows.Sum(r => r.Qty * (r.ConversionFactor > 0m ? r.ConversionFactor : 1m));
        return roundTotalToTargetPrecision
            ? Math.Round(unroundedSum, targetPrecision, MidpointRounding.AwayFromZero)
            : unroundedSum;
    }

    /// <summary>
    /// Resolves the credit/outgoing amount taken back from <c>Stock Received But Not Billed</c> when returning
    /// stock from a rejected warehouse on a non-internal Purchase Receipt return
    /// per ERPNext commits 39a75b170e, 097411fb47, and 3837f7b46a (<c>purchase_receipt/services/gl_composer.py</c>):
    /// When the original receipt row had accepted quantity (<paramref name="originalReceiptAcceptedQty"/> != 0),
    /// its valuation charges were absorbed by the accepted stock and the rejected stock was credited at net rate,
    /// so the return debits <c>Stock Received But Not Billed</c> at the receipt's net rate
    /// (<c>stockQty * baseNetRate / conversionFactor</c>) instead of the warehouse's current SLE value.
    /// </summary>
    public static decimal ResolveRejectedWarehouseReturnCreditAmount(
        bool isReturn,
        bool returnQtyFromRejectedWarehouse,
        bool isInternalTransfer,
        Guid? originalPurchaseReceiptItemId,
        decimal originalReceiptAcceptedQty,
        decimal returnStockQty,
        decimal returnBaseNetRate,
        decimal returnConversionFactor,
        decimal sleStockValueDifference)
    {
        if (isReturn &&
            returnQtyFromRejectedWarehouse &&
            !isInternalTransfer &&
            originalPurchaseReceiptItemId.HasValue &&
            originalPurchaseReceiptItemId.Value != Guid.Empty &&
            originalReceiptAcceptedQty != 0m)
        {
            var factor = returnConversionFactor > 0m ? returnConversionFactor : 1m;
            return returnStockQty * returnBaseNetRate / factor;
        }

        return sleStockValueDifference;
    }

    /// <summary>
    /// Determines whether a Purchase Receipt row (or Purchase Receipt Return row) was received at its linked
    /// Purchase Invoice row's net rate and computes the exchange rate difference discrepancy to book/reverse
    /// per ERPNext commits 1d8fad1d6b and 81f685b0f8 (<c>purchase_receipt/services/gl_composer.py</c>):
    /// A return row in another UOM checks whether its original receipt row's net rate matched the invoice row's net rate.
    /// </summary>
    public static (bool IsAtInvoiceRate, decimal ExchangeRateDiscrepancy) CalculatePurchaseReceiptInvoiceExchangeDiscrepancy(
        bool isReturn,
        decimal receiptRowQty,
        decimal receiptRowNetRate,
        decimal receiptConversionRate,
        decimal? originalReceiptRowNetRate,
        decimal invoiceRowNetRate,
        decimal invoiceConversionRate,
        int precision = 2)
    {
        var isAtInvoiceRate = isReturn
            ? (originalReceiptRowNetRate.HasValue && originalReceiptRowNetRate.Value == invoiceRowNetRate)
            : (receiptRowNetRate == invoiceRowNetRate);

        if (!isAtInvoiceRate || invoiceConversionRate <= 0m || receiptConversionRate == invoiceConversionRate)
        {
            return (isAtInvoiceRate, 0m);
        }

        var discrepancy = Math.Round(
            (receiptRowQty * receiptRowNetRate) * (receiptConversionRate - invoiceConversionRate),
            precision,
            MidpointRounding.AwayFromZero);

        return (true, discrepancy);
    }

    /// <summary>
    /// Resolves the incoming rate for an internal-transfer Purchase Receipt or Purchase Invoice row (including returns),
    /// reaching the outward delivery/invoice row through the original receipt/invoice row on a return
    /// and adding any Landed Cost Voucher rate per ERPNext commit 93f8c80982 (<c>stock_ledger.py</c>).
    /// </summary>
    public static decimal ResolveInternalTransferIncomingRate(
        decimal outwardDeliverySleStockValueDifference,
        decimal outwardDeliverySleActualQty,
        decimal landedCostVoucherAmount = 0m,
        decimal currentSleActualQty = 0m)
    {
        var baseRate = outwardDeliverySleActualQty != 0m
            ? Math.Abs(outwardDeliverySleStockValueDifference / outwardDeliverySleActualQty)
            : 0m;

        if (landedCostVoucherAmount != 0m && currentSleActualQty != 0m)
        {
            baseRate += landedCostVoucherAmount / Math.Abs(currentSleActualQty);
        }

        return baseRate;
    }

    /// <summary>
    /// Selects the latest purchase voucher for <c>get_last_purchase_details</c> across Purchase Order,
    /// Purchase Receipt, and Purchase Invoice, skipping free rows and internal supplier purchases,
    /// and tie-breaking on the same date in priority order (Purchase Order &gt; Purchase Receipt &gt; Purchase Invoice)
    /// per ERPNext commits c08a053ae3 and 9d7f0c4807 (<c>item.py</c>).
    /// </summary>
    public static (string VoucherType, DateTime PurchaseDate, decimal BaseNetRatePerStockUom)? ResolveLastPurchaseDetails(
        IEnumerable<(string VoucherType, DateTime PurchaseDate, decimal BaseNetRate, decimal ConversionFactor, bool IsFreeItem, bool IsInternalSupplier)>? candidates)
    {
        if (candidates == null)
        {
            return null;
        }

        static int GetVoucherPriority(string voucherType) =>
            voucherType.Replace(" ", string.Empty).ToUpperInvariant() switch
            {
                "PURCHASEORDER" => 3,
                "PURCHASERECEIPT" => 2,
                "PURCHASEINVOICE" => 1,
                _ => 0
            };

        var eligible = candidates
            .Where(c => !c.IsFreeItem && !c.IsInternalSupplier)
            .OrderByDescending(c => c.PurchaseDate.Date)
            .ThenByDescending(c => GetVoucherPriority(c.VoucherType))
            .ToList();

        if (eligible.Count == 0)
        {
            return null;
        }

        var winner = eligible[0];
        var factor = winner.ConversionFactor > 0m ? winner.ConversionFactor : 1m;
        return (winner.VoucherType, winner.PurchaseDate.Date, winner.BaseNetRate / factor);
    }

    /// <summary>
    /// Resolves the default supplier for a buying document from its item rows' item defaults or item group defaults,
    /// stopping at the first row that yields a default supplier per ERPNext commit 63717f769f (<c>buying_controller.py</c>).
    /// </summary>
    public static Guid? ResolveFirstDefaultSupplierFromItems(
        IEnumerable<(Guid? ItemDefaultSupplierId, Guid? ItemGroupDefaultSupplierId)>? itemRows)
    {
        if (itemRows == null)
        {
            return null;
        }

        foreach (var row in itemRows)
        {
            var supplierId = (row.ItemDefaultSupplierId.HasValue && row.ItemDefaultSupplierId.Value != Guid.Empty)
                ? row.ItemDefaultSupplierId
                : ((row.ItemGroupDefaultSupplierId.HasValue && row.ItemGroupDefaultSupplierId.Value != Guid.Empty)
                    ? row.ItemGroupDefaultSupplierId
                    : null);

            if (supplierId.HasValue)
            {
                return supplierId.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes the Stock Received But Not Billed debit/outgoing amount for a Purchase Receipt return
    /// from a rejected warehouse: when the original receipt had accepted quantity (<paramref name="originalAcceptedQty"/> &gt; 0)
    /// and is not an internal transfer, debits at the receipt's net rate (<c>stockQty * baseNetRate / conversionFactor</c>)
    /// without valuation charges and immune to later batch revaluation or standard-rate changes.
    /// Per ERPNext commits <c>097411fb47</c> and <c>3837f7b46a</c> (<c>PurchaseReceiptGLComposer</c>).
    /// </summary>
    public static decimal ResolveRejectedWarehouseReturnDebitAmount(
        bool isReturn,
        bool returnQtyFromRejectedWarehouse,
        bool isInternalTransfer,
        decimal originalAcceptedQty,
        decimal returnStockQty,
        decimal baseNetRate,
        decimal conversionFactor,
        decimal fallbackOutgoingAmount,
        int precision = 2)
    {
        if (isReturn
            && returnQtyFromRejectedWarehouse
            && !isInternalTransfer
            && originalAcceptedQty > 0m)
        {
            var factor = conversionFactor > 0m ? conversionFactor : 1m;
            return Math.Round((returnStockQty * baseNetRate) / factor, precision, MidpointRounding.AwayFromZero);
        }

        return Math.Round(fallbackOutgoingAmount, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Clears stale <c>ReceivedQty</c> and <c>RejectedQty</c> left over from <c>UpdateStock</c> once
    /// <c>UpdateStock</c> is turned off on a Purchase Invoice, while preserving rows linked to an
    /// existing Purchase Receipt item (<c>PurchaseReceiptItemId.HasValue</c>).
    /// Per ERPNext commit <c>3496853eae</c> (<c>PurchaseInvoice.reset_receipt_qty</c>).
    /// </summary>
    public static List<(Guid RowId, decimal ReceivedQty, decimal RejectedQty)> ResetUnlinkedPurchaseInvoiceReceiptQuantities(
        bool updateStock,
        IEnumerable<(Guid RowId, Guid? PurchaseReceiptItemId, decimal ReceivedQty, decimal RejectedQty)>? rows)
    {
        var result = new List<(Guid RowId, decimal ReceivedQty, decimal RejectedQty)>();
        if (rows == null)
        {
            return result;
        }

        foreach (var row in rows)
        {
            var shouldClear = !updateStock && (!row.PurchaseReceiptItemId.HasValue || row.PurchaseReceiptItemId.Value == Guid.Empty);
            result.Add((row.RowId, shouldClear ? 0m : row.ReceivedQty, shouldClear ? 0m : row.RejectedQty));
        }

        return result;
    }

    /// <summary>
    /// Computes the remaining quantity to receive on a Purchase Receipt mapped from a Purchase Invoice row:
    /// sums debit notes in stock UOM from unrounded <c>-debitQty * debitConversionFactor</c> BEFORE dividing
    /// by the invoice row's <paramref name="invoiceConversionFactor"/> (avoiding rounded stock-qty and
    /// per-row division drift), subtracts received/mapped and debited qty from <paramref name="invoiceQty"/>,
    /// rounds to <paramref name="qtyPrecision"/>, and clamps to <c>&gt;= 0</c>.
    /// Per ERPNext commits <c>4dd309aec7</c>, <c>3e5f527cfd</c>, and <c>d82d54cf42</c> (<c>purchase_invoice/mapper.py</c>).
    /// </summary>
    public static decimal CalculatePurchaseInvoicePendingReceiptQty(
        decimal invoiceQty,
        decimal invoiceConversionFactor,
        decimal receivedAndMappedQty,
        IEnumerable<(decimal DebitNoteQty, decimal DebitNoteConversionFactor)>? submittedDebitNotes,
        int qtyPrecision = 4)
    {
        var invFactor = invoiceConversionFactor > 0m ? invoiceConversionFactor : 1m;
        var returnedStockQty = 0m;
        if (submittedDebitNotes != null)
        {
            foreach (var (dnQty, dnFactor) in submittedDebitNotes)
            {
                var factor = dnFactor > 0m ? dnFactor : 1m;
                returnedStockQty += (-dnQty) * factor;
            }
        }

        var returnedInRowUom = returnedStockQty / invFactor;
        var pending = invoiceQty - receivedAndMappedQty - returnedInRowUom;
        return Math.Max(Math.Round(pending, qtyPrecision, MidpointRounding.AwayFromZero), 0m);
    }
}
