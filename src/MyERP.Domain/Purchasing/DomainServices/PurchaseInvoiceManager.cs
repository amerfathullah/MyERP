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
/// Domain service for Purchase Invoice business rules.
/// Validates return documents, supplier hold, over-billing, duplicate supplier invoice numbers,
/// and temporal ordering against linked purchase orders.
/// </summary>
public class PurchaseInvoiceManager : DomainService
{
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepository;
    private readonly IRepository<PurchaseOrder, Guid> _poRepository;

    public PurchaseInvoiceManager(
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<PurchaseInvoice, Guid> invoiceRepository,
        IRepository<PurchaseOrder, Guid> poRepository)
    {
        _supplierRepository = supplierRepository;
        _invoiceRepository = invoiceRepository;
        _poRepository = poRepository;
    }

    /// <summary>
    /// Validates PI posting date is not before any linked PO's transaction date.
    /// Per ERPNext validate_posting_date_with_po: temporal ordering enforcement.
    /// </summary>
    public async Task ValidatePostingDateWithPOAsync(PurchaseInvoice invoice)
    {
        var poIds = invoice.Items
            .Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value)
            .Distinct()
            .ToList();

        if (!poIds.Any()) return;

        // Get all linked POs via item references (PurchaseOrderItemId → find parent PO)
        var poQuery = await _poRepository.GetQueryableAsync();
        var linkedPOs = poQuery
            .Where(po => po.Items.Any(i => poIds.Contains(i.Id)))
            .Select(po => new { po.OrderNumber, po.OrderDate })
            .ToList();

        foreach (var po in linkedPOs)
        {
            if (po.OrderDate > invoice.IssueDate)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PostingDateBeforePODate)
                    .WithData("postingDate", invoice.IssueDate)
                    .WithData("poDate", po.OrderDate)
                    .WithData("poNumber", po.OrderNumber);
            }
        }
    }

    /// <summary>
    /// Validates that no submitted Assets exist on the original document before allowing return.
    /// Per DO-NOT: "Allow purchase return (PR/PI) when submitted Assets exist on the original document"
    /// </summary>
    public async Task ValidateAssetReturnAsync(
        PurchaseInvoice returnInvoice,
        IRepository<Assets.Entities.Asset, Guid> assetRepository)
    {
        if (!returnInvoice.IsReturn || !returnInvoice.ReturnAgainstId.HasValue) return;

        var assetQuery = await assetRepository.GetQueryableAsync();
        var hasSubmittedAssets = assetQuery.Any(a =>
            a.PurchaseInvoiceId == returnInvoice.ReturnAgainstId.Value
            && a.Status != Assets.AssetStatus.Draft
            && a.Status != Assets.AssetStatus.Cancelled);

        if (hasSubmittedAssets)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                .WithData("documentType", "Purchase Invoice")
                .WithData("returnAgainst", returnInvoice.ReturnAgainstId.Value);
        }
    }

    /// <summary>
    /// Validates supplier eligibility for purchase invoices.
    /// HoldType.All or HoldType.Invoices blocks PI submission.
    /// Returns (debit notes) are allowed even when supplier is on hold.
    /// </summary>
    public async Task ValidateSupplierForInvoiceAsync(Guid supplierId, bool isReturn)
    {
        if (isReturn) return; // Debit notes always allowed per ERPNext

        var supplier = await _supplierRepository.GetAsync(supplierId);

        if (supplier.HoldType == SupplierHoldType.All ||
            supplier.HoldType == SupplierHoldType.Invoices)
        {
            throw new BusinessException(MyERPDomainErrorCodes.SupplierOnHold)
                .WithData("supplierName", supplier.Name)
                .WithData("holdType", supplier.HoldType.ToString());
        }
    }

    /// <summary>
    /// Validates return invoice (debit note) business rules.
    /// Per DO-NOT: negative qty required, must reference original, exchange rate must match,
    /// return qty cannot exceed original.
    /// </summary>
    public async Task ValidateReturnAsync(PurchaseInvoice returnInvoice)
    {
        if (!returnInvoice.IsReturn) return;

        // Must have negative quantities and at least one item with negative quantity (PR #57645 / commit d44ed5357d)
        if (returnInvoice.Items.Any(i => i.Quantity > 0) || !returnInvoice.Items.Any(i => i.Quantity < 0))
        {
            throw new BusinessException("MyERP:08001")
                .WithData("documentType", "Purchase Invoice");
        }

        // Must reference original invoice
        if (!returnInvoice.ReturnAgainstId.HasValue)
        {
            throw new BusinessException("MyERP:08002")
                .WithData("documentType", "Purchase Invoice");
        }

        // Load original to validate exchange rate and qty caps
        var original = await _invoiceRepository.GetAsync(returnInvoice.ReturnAgainstId.Value);

        // Party account (credit_to) must match original
        if (returnInvoice.CreditToAccountId != Guid.Empty &&
            original.CreditToAccountId != Guid.Empty &&
            returnInvoice.CreditToAccountId != original.CreditToAccountId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnAccountMismatch)
                .WithData("documentType", "Purchase Invoice")
                .WithData("expectedAccount", original.CreditToAccountId);
        }

        if (returnInvoice.ExchangeRate != original.ExchangeRate)
        {
            throw new BusinessException("MyERP:08003")
                .WithData("expected", original.ExchangeRate)
                .WithData("actual", returnInvoice.ExchangeRate);
        }

        // Query prior submitted/posted returns against this same original invoice
        var piQuery = await _invoiceRepository.GetQueryableAsync();
        var priorReturns = piQuery
            .Where(pi => pi.ReturnAgainstId == original.Id
                && pi.Id != returnInvoice.Id
                && (pi.Status == Core.DocumentStatus.Submitted || pi.Status == Core.DocumentStatus.Posted))
            .SelectMany(pi => pi.Items)
            .ToList();

        var priorReturnedByItem = priorReturns
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity)));

        // Return qty per item cannot exceed (original qty - already_returned)
        foreach (var returnItem in returnInvoice.Items)
        {
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem == null) continue;

            var alreadyReturned = priorReturnedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
            var maxReturnable = originalItem.Quantity - alreadyReturned;

            if (Math.Abs(returnItem.Quantity) > maxReturnable)
            {
                throw new BusinessException("MyERP:08004")
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItem.Quantity)
                    .WithData("alreadyReturned", alreadyReturned)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
            }
        }
    }

    /// <summary>
    /// Validates no duplicate supplier invoice numbers exist for the same supplier + company.
    /// Prevents accidental double-entry of the same vendor bill.
    /// </summary>
    public async Task ValidateNoDuplicateSupplierInvoiceAsync(
        Guid supplierId, Guid companyId, string? supplierInvoiceNumber, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(supplierInvoiceNumber)) return;

        var query = await _invoiceRepository.GetQueryableAsync();
        var exists = query.Any(pi =>
            pi.SupplierId == supplierId
            && pi.CompanyId == companyId
            && pi.SupplierInvoiceNumber == supplierInvoiceNumber
            && pi.Status != Core.DocumentStatus.Cancelled
            && (!excludeId.HasValue || pi.Id != excludeId.Value));

        if (exists)
        {
            throw new BusinessException("MyERP:04009")
                .WithData("supplierInvoiceNumber", supplierInvoiceNumber);
        }
    }

    /// <summary>
    /// Validates that a PI cannot be cancelled if it has been paid.
    /// Must reverse payments before cancelling.
    /// </summary>
    public void ValidateCanCancel(PurchaseInvoice invoice)
    {
        if (invoice.AmountPaid > 0)
        {
            throw new BusinessException("MyERP:01002")
                .WithData("documentType", "Purchase Invoice")
                .WithData("amountPaid", invoice.AmountPaid);
        }
    }

    /// <summary>
    /// Validates that return invoices with stock effect have no zero-qty items.
    /// Stock-affecting returns MUST move stock — a zero-qty line would corrupt FIFO queues.
    /// Source: erpnext/controllers/accounts_controller.py → validate_zero_qty_for_return_invoices_with_stock
    /// </summary>
    public static void ValidateReturnWithStockNoZeroQty(PurchaseInvoice invoice)
    {
        if (!invoice.IsReturn || !invoice.UpdateStock) return;

        var zeroQtyRows = invoice.Items.Where(i => i.Quantity == 0).ToList();
        if (zeroQtyRows.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ReturnWithStockZeroQty)
                .WithData("documentType", "Purchase Invoice")
                .WithData("affectedRows", string.Join(", ", zeroQtyRows.Select((_, idx) => $"#{idx + 1}")));
        }
    }

    /// <summary>
    /// Mandatory PO linkage: every PI item must reference a Purchase Order.
    /// Per ERPNext accounts/doctype/purchase_invoice/purchase_invoice.py → po_required().
    /// Skipped for return invoices.
    /// </summary>
    public static void ValidatePoRequired(PurchaseInvoice invoice, bool poRequired)
    {
        if (!poRequired || invoice.IsReturn) return;

        var unlinkedItem = invoice.Items.FirstOrDefault(i => !i.PurchaseOrderItemId.HasValue);
        if (unlinkedItem != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PurchaseOrderLinkRequired)
                .WithData("itemDescription", unlinkedItem.Description);
        }
    }

    /// <summary>
    /// Mandatory PR linkage: every stock/asset PI item must reference a Purchase Receipt.
    /// Per ERPNext accounts/doctype/purchase_invoice/purchase_invoice.py → pr_required().
    /// Distinct from <see cref="ValidateThreeWayMatching"/>, which checks billed qty against
    /// received qty for items that already have a link — this checks the link itself exists.
    /// Skipped for return invoices.
    /// </summary>
    /// <param name="isStockItem">Resolves whether an item requires stock (Item.MaintainStock).</param>
    public static void ValidatePrRequiredLinkage(
        PurchaseInvoice invoice, bool prRequired, Func<Guid, bool> isStockItem)
    {
        if (!prRequired || invoice.IsReturn) return;

        var unlinkedItem = invoice.Items.FirstOrDefault(
            i => !i.PurchaseReceiptItemId.HasValue && isStockItem(i.ItemId));
        if (unlinkedItem != null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PurchaseReceiptLinkRequired)
                .WithData("itemDescription", unlinkedItem.Description);
        }
    }

    /// <summary>
    /// 3-Way Matching Validation: validates PI items against PR received qty.
    /// Per ERPNext buying_controller.validate_received_qty:
    /// - If pr_required is enabled (Buying Settings), PI cannot bill more than received.
    /// - Compares PI item qty against sum of PR items received for the same PO item.
    /// This prevents billing fraud (invoicing before goods are verified received).
    /// </summary>
    /// <param name="invoice">Purchase Invoice to validate</param>
    /// <param name="getReceivedQtyForPOItem">
    /// Function that returns total received qty for a specific PO item ID
    /// (resolved from PurchaseReceipt items linked to the same PO item).
    /// </param>
    /// <param name="prRequired">Whether Purchase Receipt is required before PI (from Buying Settings)</param>
    public static void ValidateThreeWayMatching(
        PurchaseInvoice invoice,
        Func<Guid, decimal> getReceivedQtyForPOItem,
        bool prRequired = false)
    {
        if (!prRequired || invoice.IsReturn) return;

        foreach (var item in invoice.Items)
        {
            if (!item.PurchaseOrderItemId.HasValue) continue;

            var receivedQty = getReceivedQtyForPOItem(item.PurchaseOrderItemId.Value);
            var alreadyBilledQty = 0m; // Would be resolved from existing PI items for same PO item

            var maxBillableQty = receivedQty - alreadyBilledQty;
            if (item.Quantity > maxBillableQty && maxBillableQty >= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ThreeWayMatchingFailed)
                    .WithData("itemDescription", item.Description)
                    .WithData("invoicedQty", item.Quantity)
                    .WithData("receivedQty", receivedQty)
                    .WithData("maxBillableQty", maxBillableQty);
            }
        }
    }

    /// <summary>
    /// Updates linked Purchase Receipt item BilledQty after PI submit.
    /// Per ERPNext: update_billed_amount_in_pr updates PR Item billed_amt using FIFO.
    /// When reverse=true (cancel), decrements BilledQty.
    /// </summary>
    public async Task UpdateLinkedPurchaseReceiptBillingAsync(PurchaseInvoice invoice, bool reverse = false)
    {
        var prItemIds = invoice.Items
            .Where(i => i.PurchaseReceiptItemId.HasValue)
            .Select(i => i.PurchaseReceiptItemId!.Value)
            .Distinct()
            .ToList();

        if (!prItemIds.Any()) return;

        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQuery = await prRepo.GetQueryableAsync();
        var affectedPrs = prQuery
            .Where(pr => pr.Items.Any(i => prItemIds.Contains(i.Id)))
            .ToList();

        foreach (var pr in affectedPrs)
        {
            foreach (var piItem in invoice.Items.Where(i => i.PurchaseReceiptItemId.HasValue))
            {
                var prItem = pr.Items.FirstOrDefault(i => i.Id == piItem.PurchaseReceiptItemId!.Value);
                if (prItem != null)
                {
                    // Per ERPNext billing_status.py: amount_difference_with_purchase_invoice =
                    // (pi_billed_rate - pr_rate) × billed_qty — tracks how much this PI's rate
                    // diverged from what was recorded at receipt time, accumulated across every
                    // PI that bills against this row.
                    var variance = (piItem.UnitPrice - prItem.UnitPrice) * Math.Abs(piItem.Quantity);

                    if (reverse)
                    {
                        prItem.BilledQty = Math.Max(0, prItem.BilledQty - Math.Abs(piItem.Quantity));
                        prItem.AmountDifferenceWithPurchaseInvoice -= variance;
                    }
                    else
                    {
                        prItem.BilledQty += Math.Abs(piItem.Quantity);
                        prItem.AmountDifferenceWithPurchaseInvoice += variance;
                    }
                }
            }
            await prRepo.UpdateAsync(pr, autoSave: true);
        }
    }

    /// <summary>
    /// Validates exchange rate parity between Purchase Invoice and linked Purchase Receipts.
    /// Per ERPNext PR #58177: when billing against a PR under perpetual inventory, the PI exchange rate
    /// must match the PR exchange rate unless set_landed_cost_based_on_purchase_invoice_rate is enabled.
    /// </summary>
    public async Task ValidateExchangeRateWithPurchaseReceiptAsync(
        PurchaseInvoice invoice,
        IRepository<PurchaseReceipt, Guid> prRepository,
        bool isPerpetualInventory = true,
        bool setLandedCostBasedOnPiRate = false)
    {
        if (!isPerpetualInventory || setLandedCostBasedOnPiRate || invoice.IsReturn) return;

        var prItemIds = invoice.Items
            .Where(i => i.PurchaseReceiptItemId.HasValue)
            .Select(i => i.PurchaseReceiptItemId!.Value)
            .Distinct()
            .ToList();

        if (!prItemIds.Any()) return;

        var prQuery = await prRepository.GetQueryableAsync();
        var linkedPrs = prQuery
            .Where(pr => pr.Items.Any(pri => prItemIds.Contains(pri.Id)))
            .ToList();

        foreach (var pr in linkedPrs)
        {
            if (pr.CurrencyCode == invoice.CurrencyCode && pr.ExchangeRate > 0 && pr.ExchangeRate != invoice.ExchangeRate)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnExchangeRateMismatch)
                    .WithData("expected", pr.ExchangeRate)
                    .WithData("actual", invoice.ExchangeRate)
                    .WithData("purchaseReceiptNumber", pr.ReceiptNumber ?? pr.Id.ToString());
            }
        }
    }
}
