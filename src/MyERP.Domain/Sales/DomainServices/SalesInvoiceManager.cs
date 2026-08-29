using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for Sales Invoice business rules.
/// Validates return documents (credit notes), over-billing, cancel guards,
/// and credit note outstanding reduction.
/// Mirrors PurchaseInvoiceManager for purchasing parity.
/// </summary>
public class SalesInvoiceManager : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _orderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public SalesInvoiceManager(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid> orderRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Mandatory SO linkage: every SI item must reference a Sales Order.
    /// Per ERPNext accounts/doctype/sales_invoice/sales_invoice.py → so_dn_required().
    /// Skipped for returns and POS/opening invoices (which post stock immediately with no SO).
    /// </summary>
    public static void ValidateSoRequired(SalesInvoice invoice, bool soRequired)
    {
        if (!soRequired || invoice.IsReturn || invoice.UpdateStock) return;

        var unlinkedItem = invoice.Items.FirstOrDefault(i => !i.SalesOrderItemId.HasValue);
        if (unlinkedItem != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SalesOrderLinkRequired)
                .WithData("itemDescription", unlinkedItem.Description);
        }
    }

    /// <summary>
    /// Mandatory DN linkage: every SI item must reference a Delivery Note.
    /// Per ERPNext accounts/doctype/sales_invoice/sales_invoice.py → so_dn_required().
    /// Skipped for returns and invoices that update stock directly (no separate DN in that flow).
    /// </summary>
    public static void ValidateDnRequired(SalesInvoice invoice, bool dnRequired)
    {
        if (!dnRequired || invoice.IsReturn || invoice.UpdateStock) return;

        var unlinkedItem = invoice.Items.FirstOrDefault(i => !i.DeliveryNoteItemId.HasValue);
        if (unlinkedItem != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DeliveryNoteLinkRequired)
                .WithData("itemDescription", unlinkedItem.Description);
        }
    }

    /// <summary>
    /// Validates return invoice (credit note) business rules.
    /// Per DO-NOT: negative qty required, must reference original, exchange rate must match,
    /// return qty cannot exceed original.
    /// </summary>
    public async Task ValidateReturnAsync(SalesInvoice returnInvoice)
    {
        if (!returnInvoice.IsReturn) return;

        // Returns must have negative quantities
        if (returnInvoice.Items.Any(i => i.Quantity > 0))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyMustBeNegative)
                .WithData("documentType", "Sales Invoice");
        }

        // Must reference an original invoice
        if (!returnInvoice.ReturnAgainstId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnMustReferenceOriginal)
                .WithData("documentType", "Sales Invoice");
        }

        var original = await _invoiceRepository.GetAsync(returnInvoice.ReturnAgainstId.Value);

        // Party account (debit_to) must match original
        if (returnInvoice.DebitToAccountId != Guid.Empty &&
            original.DebitToAccountId != Guid.Empty &&
            returnInvoice.DebitToAccountId != original.DebitToAccountId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnAccountMismatch)
                .WithData("documentType", "Sales Invoice")
                .WithData("expectedAccount", original.DebitToAccountId);
        }

        // Exchange rate must match original document
        if (returnInvoice.ExchangeRate != original.ExchangeRate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnExchangeRateMismatch)
                .WithData("expected", original.ExchangeRate)
                .WithData("actual", returnInvoice.ExchangeRate);
        }

        // Query prior submitted/posted returns against this same original invoice
        var siQuery = await _invoiceRepository.GetQueryableAsync();
        var priorReturns = siQuery
            .Where(si => si.ReturnAgainstId == original.Id
                && si.Id != returnInvoice.Id
                && (si.Status == Core.DocumentStatus.Submitted || si.Status == Core.DocumentStatus.Posted))
            .SelectMany(si => si.Items)
            .ToList();

        var priorReturnedByItem = priorReturns
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity)));

        // Return qty per item cannot exceed (original qty - already_returned); return rate cannot exceed original
        // rate (Moving Average valuation items are exempt — their rate legitimately fluctuates).
        foreach (var returnItem in returnInvoice.Items)
        {
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem == null) continue;

            var alreadyReturned = priorReturnedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
            var maxReturnable = originalItem.Quantity - alreadyReturned;

            if (Math.Abs(returnItem.Quantity) > maxReturnable)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItem.Quantity)
                    .WithData("alreadyReturned", alreadyReturned)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
            }

            if (returnItem.UnitPrice > originalItem.UnitPrice)
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
    /// Validates over-billing: SI item qty cannot cause SO BilledQty to exceed ordered qty,
    /// including the company's over-billing tolerance percentage.
    /// Per ERPNext StatusUpdater: max_allowed = ordered_qty × (1 + allowance_pct / 100).
    /// Only applies to non-return invoices linked to a Sales Order.
    /// </summary>
    public async Task ValidateOverBillingAsync(SalesInvoice invoice, decimal overBillingAllowancePct = 0m)
    {
        if (invoice.IsReturn) return;

        var soItemIds = invoice.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (!soItemIds.Any()) return;

        var orderQuery = await _orderRepository.GetQueryableAsync();
        var affectedOrders = orderQuery
            .Where(so => so.Items.Any(soi => soItemIds.Contains(soi.Id)))
            .ToList();

        foreach (var so in affectedOrders)
        {
            foreach (var siItem in invoice.Items.Where(i => i.SalesOrderItemId.HasValue))
            {
                var soItem = so.Items.FirstOrDefault(i => i.Id == siItem.SalesOrderItemId!.Value);
                if (soItem == null) continue;

                var maxAllowedTotal = soItem.Quantity * (1m + overBillingAllowancePct / 100m);
                if (soItem.BilledQty + siItem.Quantity > maxAllowedTotal)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.OverBilling)
                        .WithData("itemName", siItem.Description)
                        .WithData("orderedQty", soItem.Quantity)
                        .WithData("billedQty", soItem.BilledQty)
                        .WithData("attemptedQty", siItem.Quantity);
                }
            }
        }
    }

    /// <summary>
    /// Updates linked Sales Order BilledQty and fulfillment status after SI submit.
    /// </summary>
    public async Task UpdateLinkedOrderBillingAsync(SalesInvoice invoice, bool reverse = false)
    {
        var soItemIds = invoice.Items
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (!soItemIds.Any()) return;

        var orderQuery = await _orderRepository.GetQueryableAsync();
        var affectedOrders = orderQuery
            .Where(so => so.Items.Any(soi => soItemIds.Contains(soi.Id)))
            .ToList();

        foreach (var so in affectedOrders)
        {
            foreach (var siItem in invoice.Items.Where(i => i.SalesOrderItemId.HasValue))
            {
                var soItem = so.Items.FirstOrDefault(i => i.Id == siItem.SalesOrderItemId!.Value);
                if (soItem != null)
                {
                    if (reverse)
                        soItem.BilledQty = Math.Max(0, soItem.BilledQty - siItem.Quantity);
                    else
                        soItem.BilledQty += siItem.Quantity;
                }
            }
            so.UpdateFulfillmentStatus();
            await _orderRepository.UpdateAsync(so, autoSave: true);
        }
    }

    /// <summary>
    /// Applies credit note outstanding reduction to the original invoice.
    /// Note: caller should wrap with concurrency retry at AppService level.
    /// </summary>
    public async Task ApplyCreditNoteAsync(SalesInvoice creditNote)
    {
        if (!creditNote.IsReturn || !creditNote.ReturnAgainstId.HasValue) return;

        var original = await _invoiceRepository.GetAsync(creditNote.ReturnAgainstId.Value);
        original.AmountPaid += Math.Abs(creditNote.GrandTotal);
        await _invoiceRepository.UpdateAsync(original, autoSave: true);
    }

    /// <summary>
    /// Validates that an SI cannot be cancelled if it has been paid.
    /// Must reverse payments before cancelling.
    /// </summary>
    public void ValidateCanCancel(SalesInvoice invoice)
    {
        if (invoice.AmountPaid > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithPayments)
                .WithData("documentType", "Sales Invoice")
                .WithData("amountPaid", invoice.AmountPaid);
        }
    }

    /// <summary>
    /// Validates that return invoices with stock effect have no zero-qty items.
    /// Stock-affecting returns MUST move stock — a zero-qty line would create
    /// valueless SLE entries corrupting FIFO queues.
    /// Source: erpnext/controllers/accounts_controller.py → validate_zero_qty_for_return_invoices_with_stock
    /// </summary>
    public static void ValidateReturnWithStockNoZeroQty(SalesInvoice invoice)
    {
        if (!invoice.IsReturn || !invoice.UpdateStock) return;

        var zeroQtyRows = invoice.Items.Where(i => i.Quantity == 0).ToList();
        if (zeroQtyRows.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnWithStockZeroQty)
                .WithData("documentType", "Sales Invoice")
                .WithData("affectedRows", string.Join(", ", zeroQtyRows.Select((_, idx) => $"#{idx + 1}")));
        }
    }

    /// <summary>
    /// Validates selling price is not below buying/valuation rate.
    /// Per ERPNext validate_selling_price (Selling Settings.validate_selling_price).
    /// Action: "Stop" = hard error, "Warn" = soft warning (allow but flag).
    /// </summary>
    public static SellingPriceCheckResult ValidateSellingPrice(
        IReadOnlyList<SalesInvoiceItem> items,
        Func<Guid, decimal> getValuationRate,
        string action = "Stop")
    {
        var itemData = items.Select(i => (i.ItemId, i.UnitPrice, i.Description)).ToList();
        return ValidateSellingPrice(itemData, getValuationRate, action);
    }

    /// <summary>
    /// Validates selling price for any document type (SO, SI, DN, Quotation).
    /// Accepts a generic list of (ItemId, UnitPrice, Description) for cross-document reuse.
    /// </summary>
    public static SellingPriceCheckResult ValidateSellingPrice(
        IReadOnlyList<(Guid ItemId, decimal UnitPrice, string Description)> items,
        Func<Guid, decimal> getValuationRate,
        string action = "Stop")
    {
        var warnings = new List<string>();

        foreach (var item in items)
        {
            var valuationRate = getValuationRate(item.ItemId);
            if (valuationRate <= 0) continue; // no cost data → skip

            if (item.UnitPrice < valuationRate)
            {
                var message = $"Item '{item.Description}' selling rate ({item.UnitPrice:N2}) is below buying/valuation rate ({valuationRate:N2})";

                if (action == "Stop")
                {
                    throw new BusinessException(MyERPDomainErrorCodes.SellingPriceBelowCost)
                        .WithData("item", item.Description)
                        .WithData("sellingRate", item.UnitPrice)
                        .WithData("buyingRate", valuationRate);
                }

                warnings.Add(message);
            }
        }

        return new SellingPriceCheckResult { Warnings = warnings };
    }
    /// <summary>
    /// Async overload for ValidateSellingPrice that avoids sync-over-async in calling code.
    /// Resolves valuation rates asynchronously before validation.
    /// </summary>
    public static async Task<SellingPriceCheckResult> ValidateSellingPriceAsync(
        IReadOnlyList<(Guid ItemId, decimal UnitPrice, string Description)> items,
        Func<Guid, Task<decimal>> getValuationRateAsync,
        string action = "Stop")
    {
        var warnings = new List<string>();

        foreach (var item in items)
        {
            var valuationRate = await getValuationRateAsync(item.ItemId);
            if (valuationRate <= 0) continue;

            if (item.UnitPrice < valuationRate)
            {
                var message = $"Item '{item.Description}' selling rate ({item.UnitPrice:N2}) is below buying/valuation rate ({valuationRate:N2})";

                if (action == "Stop")
                {
                    throw new BusinessException(MyERPDomainErrorCodes.SellingPriceBelowCost)
                        .WithData("item", item.Description)
                        .WithData("sellingRate", item.UnitPrice)
                        .WithData("buyingRate", valuationRate);
                }

                warnings.Add(message);
            }
        }

        return new SellingPriceCheckResult { Warnings = warnings };
    }

    /// <summary>
    /// Updates linked Delivery Note item BilledQty after SI submit.
    /// Per ERPNext: update_billed_amount_in_dn updates DN Item billed_amt using FIFO.
    /// When reverse=true (cancel), decrements BilledQty.
    /// </summary>
    public async Task UpdateLinkedDeliveryNoteBillingAsync(SalesInvoice invoice, bool reverse = false)
    {
        var dnItemIds = invoice.Items
            .Where(i => i.DeliveryNoteItemId.HasValue)
            .Select(i => i.DeliveryNoteItemId!.Value)
            .Distinct()
            .ToList();

        if (!dnItemIds.Any()) return;

        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var dnQuery = await dnRepo.GetQueryableAsync();
        var affectedDns = dnQuery
            .Where(dn => dn.Items.Any(i => dnItemIds.Contains(i.Id)))
            .ToList();

        foreach (var dn in affectedDns)
        {
            foreach (var siItem in invoice.Items.Where(i => i.DeliveryNoteItemId.HasValue))
            {
                var dnItem = dn.Items.FirstOrDefault(i => i.Id == siItem.DeliveryNoteItemId!.Value);
                if (dnItem != null)
                {
                    if (reverse)
                        dnItem.BilledQty = Math.Max(0, dnItem.BilledQty - Math.Abs(siItem.Quantity));
                    else
                        dnItem.BilledQty += Math.Abs(siItem.Quantity);
                }
            }
            await dnRepo.UpdateAsync(dn, autoSave: true);
        }
    }
}

/// <summary>Result of selling price validation. Contains warnings when action is "Warn".</summary>
public class SellingPriceCheckResult
{
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
    public bool HasWarnings => Warnings.Count > 0;
}
