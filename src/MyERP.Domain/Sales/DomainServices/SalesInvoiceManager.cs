using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public SalesInvoiceManager(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid> orderRepository)
    {
        _invoiceRepository = invoiceRepository;
        _orderRepository = orderRepository;
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

        // Exchange rate must match original document
        if (returnInvoice.ExchangeRate != original.ExchangeRate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnExchangeRateMismatch)
                .WithData("expected", original.ExchangeRate)
                .WithData("actual", returnInvoice.ExchangeRate);
        }

        // Return qty per item cannot exceed original qty
        foreach (var returnItem in returnInvoice.Items)
        {
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem != null && Math.Abs(returnItem.Quantity) > originalItem.Quantity)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal)
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItem.Quantity)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
            }
        }
    }

    /// <summary>
    /// Validates over-billing: SI item qty cannot cause SO BilledQty to exceed ordered qty.
    /// Only applies to non-return invoices linked to a Sales Order.
    /// </summary>
    public async Task ValidateOverBillingAsync(SalesInvoice invoice)
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
                if (soItem != null && (soItem.BilledQty + siItem.Quantity) > soItem.Quantity)
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
            await _orderRepository.UpdateAsync(so);
        }
    }

    /// <summary>
    /// Applies credit note outstanding reduction to the original invoice.
    /// </summary>
    public async Task ApplyCreditNoteAsync(SalesInvoice creditNote)
    {
        if (!creditNote.IsReturn || !creditNote.ReturnAgainstId.HasValue) return;

        var original = await _invoiceRepository.GetAsync(creditNote.ReturnAgainstId.Value);
        original.AmountPaid += Math.Abs(creditNote.GrandTotal);
        await _invoiceRepository.UpdateAsync(original);
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
}

/// <summary>Result of selling price validation. Contains warnings when action is "Warn".</summary>
public class SellingPriceCheckResult
{
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
    public bool HasWarnings => Warnings.Count > 0;
}
