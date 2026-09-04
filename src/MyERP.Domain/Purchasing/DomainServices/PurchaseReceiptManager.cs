using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public PurchaseReceiptManager(
        IRepository<PurchaseOrder, Guid> poRepository,
        IRepository<PurchaseReceipt, Guid> prRepository)
    {
        _poRepository = poRepository;
        _prRepository = prRepository;
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
        if (!receipt.PurchaseOrderId.HasValue) return;

        var po = await _poRepository.GetAsync(receipt.PurchaseOrderId.Value);

        // PO must be in an active fulfillment state (returns allowed against Closed PO, but blocked against Cancelled)
        if (po.Status == Core.DocumentStatus.Cancelled || (!receipt.IsReturn && po.Status == Core.DocumentStatus.Closed))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", "Purchase Order")
                .WithData("status", po.Status.ToString());
        }

        // Return PRs bypass temporal ordering and pending quantity checks against PO
        if (receipt.IsReturn) return;

        // Temporal ordering: cannot receive before ordering
        if (receipt.PostingDate < po.OrderDate)
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
                : po.Items.FirstOrDefault(i => i.ItemId == prItem.ItemId);
            if (poItem != null)
            {
                if (poItem.IsClosed)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Item {prItem.Description} is closed in Purchase Order {po.OrderNumber} and cannot be processed further.");
                }
                if (prItem.Quantity > poItem.PendingReceiptQty)
                {
                    throw new BusinessException("MyERP:08006")
                        .WithData("itemName", prItem.Description)
                        .WithData("orderedQty", poItem.Quantity)
                        .WithData("receivedQty", poItem.ReceivedQty)
                        .WithData("attemptedQty", prItem.Quantity);
                }
            }
        }
    }

    /// <summary>
    /// Validates that no submitted Assets exist on the original document before allowing return.
    /// Per DO-NOT: "Allow purchase return (PR/PI) when submitted Assets exist on the original document"
    /// </summary>
    public async Task ValidateAssetReturnAsync(
        PurchaseReceipt returnReceipt,
        IRepository<Asset, Guid> assetRepository)
    {
        if (!returnReceipt.IsReturn || !returnReceipt.ReturnAgainstId.HasValue) return;

        var assetQuery = await assetRepository.GetQueryableAsync();
        var hasSubmittedAssets = assetQuery.Any(a =>
            a.PurchaseReceiptId == returnReceipt.ReturnAgainstId.Value
            && a.Status != Assets.AssetStatus.Draft
            && a.Status != Assets.AssetStatus.Cancelled);

        if (hasSubmittedAssets)
        {
            throw new BusinessException(MyERPDomainErrorCodes.AssetExistsOnReturnDocument)
                .WithData("documentType", "Purchase Receipt")
                .WithData("returnAgainst", returnReceipt.ReturnAgainstId.Value);
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

        // Must have negative quantities and at least one item with negative quantity (PR #57645 / commit d44ed5357d)
        if (returnReceipt.Items.Any(i => i.Quantity > 0) || !returnReceipt.Items.Any(i => i.Quantity < 0))
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
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => Math.Abs(i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m))));

        // Return qty per item cannot exceed (original qty - already_returned)
        // Uses stock qty comparison to support different UOM returns (ERPNext commit abf94bc72d).
        foreach (var returnItem in returnReceipt.Items)
        {
            var originalItem = original.Items.FirstOrDefault(i => i.ItemId == returnItem.ItemId);
            if (originalItem == null) continue;

            var returnFactor = returnItem.ConversionFactor > 0 ? returnItem.ConversionFactor : 1m;
            var originalFactor = originalItem.ConversionFactor > 0 ? originalItem.ConversionFactor : 1m;
            var originalStockQty = originalItem.Quantity * originalFactor;
            var returnStockQty = Math.Abs(returnItem.Quantity) * returnFactor;

            var alreadyReturnedStock = priorReturnedByItem.GetValueOrDefault(returnItem.ItemId, 0m);
            var maxReturnableStock = originalStockQty - alreadyReturnedStock;

            if (returnStockQty > maxReturnableStock + 0.0000001m)
            {
                throw new BusinessException("MyERP:08004")
                    .WithData("itemName", returnItem.Description)
                    .WithData("originalQty", originalItem.Quantity)
                    .WithData("alreadyReturned", alreadyReturnedStock / returnFactor)
                    .WithData("returnQty", Math.Abs(returnItem.Quantity));
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
}
